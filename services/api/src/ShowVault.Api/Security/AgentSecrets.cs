using System.Security.Cryptography;
using System.Text;

namespace ShowVault.Api.Security;

public static class AgentSecrets
{
    private const int SecretByteCount = 32;
    private const int HexCharacterCount = SecretByteCount * 2;

    public static string Generate(string prefix) =>
        $"{prefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretByteCount)).ToLowerInvariant()}";

    public static byte[] Hash(string secret) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public static bool IsWellFormedCredentialSecret(string? secret)
    {
        if (secret is null ||
            secret.Length != "sva_".Length + HexCharacterCount ||
            !secret.StartsWith("sva_", StringComparison.Ordinal))
        {
            return false;
        }

        return secret.AsSpan("sva_".Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    public static bool Verify(string secret, byte[] expectedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
}
