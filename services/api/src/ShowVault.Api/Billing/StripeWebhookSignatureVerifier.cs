namespace ShowVault.Api.Billing;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public sealed class StripeWebhookOptions
{
    public const string SectionName = "Billing:Webhook";
    public List<string> EndpointSecrets { get; set; } = [];
    public int TimestampToleranceSeconds { get; set; } = 300;
    public int MaximumBodyBytes { get; set; } = 256 * 1024;
    public int MaximumSignatureHeaderBytes { get; set; } = 2048;
}

public interface IStripeWebhookSignatureVerifier
{
    bool Verify(ReadOnlySpan<byte> body, string signatureHeader,
        DateTimeOffset now, StripeWebhookOptions options);
}

public sealed class StripeWebhookSignatureVerifier : IStripeWebhookSignatureVerifier
{
    public bool Verify(ReadOnlySpan<byte> body, string signatureHeader,
        DateTimeOffset now, StripeWebhookOptions options)
    {
        if (body.IsEmpty || options.EndpointSecrets.Count is < 1 or > 2 ||
            options.TimestampToleranceSeconds is < 30 or > 600 ||
            Encoding.UTF8.GetByteCount(signatureHeader) > options.MaximumSignatureHeaderBytes)
            return false;

        long? timestamp = null;
        var candidates = new List<byte[]>();
        foreach (var component in signatureHeader.Split(','))
        {
            var pair = component.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "t" && long.TryParse(pair[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var parsed)) timestamp = parsed;
            if (pair[0] == "v1" && pair[1].Length == 64)
            {
                try { candidates.Add(Convert.FromHexString(pair[1])); }
                catch (FormatException) { return false; }
            }
        }
        if (timestamp is null || candidates.Count == 0 ||
            Math.Abs(now.ToUnixTimeSeconds() - timestamp.Value) >
            options.TimestampToleranceSeconds) return false;

        var prefix = Encoding.UTF8.GetBytes(
            timestamp.Value.ToString(CultureInfo.InvariantCulture) + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        foreach (var secret in options.EndpointSecrets)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length > 255) return false;
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
            foreach (var candidate in candidates)
                if (candidate.Length == expected.Length &&
                    CryptographicOperations.FixedTimeEquals(candidate, expected)) return true;
        }
        return false;
    }
}
