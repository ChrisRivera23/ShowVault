using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.Account;

public sealed record IssuedInvitationToken(string Code, byte[] Digest, string KeyId);
public sealed record InvitationTokenDigest(string KeyId, byte[] Digest);

public sealed class InvitationTokenService(IOptions<AccountInvitationOptions> options)
{
    private const int CodeBytes = 32;
    private readonly AccountInvitationOptions _options = options.Value;

    public bool IsAvailable
    {
        get
        {
            if (!TryKeys(out var keys, out _)) return false;
            Clear(keys);
            return true;
        }
    }

    public IReadOnlySet<string> ConfiguredKeyIds
    {
        get
        {
            if (!TryKeys(out var keys, out _))
                return new HashSet<string>(StringComparer.Ordinal);
            try { return keys.Select(key => key.Id).ToHashSet(StringComparer.Ordinal); }
            finally { Clear(keys); }
        }
    }

    public IssuedInvitationToken Issue()
    {
        if (!TryKeys(out var keys, out var active))
            throw new InvalidOperationException("Invitation token configuration is unavailable.");
        var bytes = RandomNumberGenerator.GetBytes(CodeBytes);
        try
        {
            var code = Base64UrlEncode(bytes);
            return new IssuedInvitationToken(code, Digest(active.Secret, bytes), active.Id);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Clear(keys);
        }
    }

    public IReadOnlyList<InvitationTokenDigest> CandidateDigests(string code)
    {
        if (!TryKeys(out var keys, out _))
            return [];
        try
        {
            if (string.IsNullOrWhiteSpace(code) ||
                Encoding.UTF8.GetByteCount(code) > _options.MaximumCodeBytes ||
                code.Length != 43 || !TryBase64UrlDecode(code, out var bytes))
                return [];
            try
            {
                if (bytes.Length != CodeBytes) return [];
                return keys.Select(key => new InvitationTokenDigest(
                    key.Id, Digest(key.Secret, bytes))).ToArray();
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally { Clear(keys); }
    }

    private bool TryKeys(out IReadOnlyList<KeyMaterial> keys, out KeyMaterial active)
    {
        keys = [];
        active = default;
        if (!_options.Enabled || _options.LifetimeHours != 168 ||
            _options.MaximumCodeBytes is < 43 or > 64 ||
            string.IsNullOrWhiteSpace(_options.ActiveKeyId) ||
            _options.Keys is not { Count: >= 1 and <= 2 })
            return false;

        var material = new List<KeyMaterial>(_options.Keys.Count);
        foreach (var key in _options.Keys)
        {
            var id = key.Id?.Trim() ?? "";
            if (id.Length is < 1 or > 80 ||
                material.Any(value => value.Id == id)) return Reject(material);
            if (string.IsNullOrWhiteSpace(key.SecretBase64)) return Reject(material);
            byte[] secret;
            try { secret = Convert.FromBase64String(key.SecretBase64); }
            catch (FormatException) { return Reject(material); }
            if (secret.Length != 32)
            {
                CryptographicOperations.ZeroMemory(secret);
                return Reject(material);
            }
            material.Add(new KeyMaterial(id, secret));
        }
        var activeId = _options.ActiveKeyId.Trim();
        if (activeId.Length is < 1 or > 80) return Reject(material);
        var resolved = material.FirstOrDefault(value => value.Id == activeId);
        if (resolved == default) return Reject(material);
        keys = material;
        active = resolved;
        return true;
    }

    private static byte[] Digest(byte[] secret, byte[] code) =>
        HMACSHA256.HashData(secret, code);

    private static bool Reject(IReadOnlyList<KeyMaterial> keys)
    {
        Clear(keys);
        return false;
    }

    private static void Clear(IReadOnlyList<KeyMaterial> keys)
    {
        foreach (var key in keys)
            CryptographicOperations.ZeroMemory(key.Secret);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_')))
            return false;
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        try { bytes = Convert.FromBase64String(base64); return true; }
        catch (FormatException) { return false; }
    }

    private readonly record struct KeyMaterial(string Id, byte[] Secret);
}
