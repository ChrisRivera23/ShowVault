using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.Account;

public sealed record IssuedInvitationToken(string Code, byte[] Digest, string KeyId);
public sealed record InvitationTokenDigest(string KeyId, byte[] Digest);

public sealed class InvitationTokenService(IOptions<AccountInvitationOptions> options)
{
    private const int CodeBytes = 32;
    private readonly AccountInvitationOptions _options = options.Value;

    public bool IsAvailable => TryKeys(out _, out _);

    public IssuedInvitationToken Issue()
    {
        if (!TryKeys(out var keys, out var active))
            throw new InvalidOperationException("Invitation token configuration is unavailable.");
        var bytes = RandomNumberGenerator.GetBytes(CodeBytes);
        var code = Base64UrlEncode(bytes);
        return new IssuedInvitationToken(code, Digest(active.Secret, bytes), active.Id);
    }

    public IReadOnlyList<InvitationTokenDigest> CandidateDigests(string code)
    {
        if (!TryKeys(out var keys, out _) || string.IsNullOrWhiteSpace(code) ||
            code.Length != 43 || !TryBase64UrlDecode(code, out var bytes) || bytes.Length != CodeBytes)
            return [];
        return keys.Select(key => new InvitationTokenDigest(
            key.Id, Digest(key.Secret, bytes))).ToArray();
    }

    private bool TryKeys(out IReadOnlyList<KeyMaterial> keys, out KeyMaterial active)
    {
        keys = [];
        active = default;
        if (!_options.Enabled || _options.LifetimeHours != 168 ||
            _options.MaximumCodeBytes is < 43 or > 64 ||
            string.IsNullOrWhiteSpace(_options.ActiveKeyId) ||
            _options.Keys.Count is < 1 or > 2 ||
            _options.Keys.Any(key => string.IsNullOrWhiteSpace(key.Id)) ||
            _options.Keys.Select(key => key.Id).Distinct(StringComparer.Ordinal).Count() !=
                _options.Keys.Count)
            return false;

        var material = new List<KeyMaterial>(_options.Keys.Count);
        foreach (var key in _options.Keys)
        {
            byte[] secret;
            try { secret = Convert.FromBase64String(key.SecretBase64); }
            catch (FormatException) { return false; }
            if (secret.Length != 32) return false;
            material.Add(new KeyMaterial(key.Id.Trim(), secret));
        }
        var resolved = material.SingleOrDefault(value =>
            value.Id == _options.ActiveKeyId.Trim());
        if (resolved == default) return false;
        keys = material;
        active = resolved;
        return true;
    }

    private static byte[] Digest(byte[] secret, byte[] code) =>
        HMACSHA256.HashData(secret, code);

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
