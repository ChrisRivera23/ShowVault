using System.Security.Cryptography;
using System.Text;

namespace ShowVault.Api.Security;

public static class AgentSecrets
{
    private const int SecretByteCount = 32;

    public static string Generate(string prefix) =>
        $"{prefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretByteCount)).ToLowerInvariant()}";

    public static byte[] Hash(string secret) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public static bool Verify(string secret, byte[] expectedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
}
