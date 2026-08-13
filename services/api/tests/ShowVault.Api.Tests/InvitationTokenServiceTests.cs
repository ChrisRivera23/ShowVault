using Microsoft.Extensions.Options;
using ShowVault.Api.Account;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class InvitationTokenServiceTests
{
    [Fact]
    public void Issued_code_has_exact_shape_and_only_digest_matches()
    {
        var service = Service(Key("active", 1), active: "active");

        var issued = service.Issue();

        Assert.Equal(43, issued.Code.Length);
        Assert.DoesNotContain("=", issued.Code, StringComparison.Ordinal);
        Assert.Equal(32, issued.Digest.Length);
        Assert.Equal("active", issued.KeyId);
        var candidate = Assert.Single(service.CandidateDigests(issued.Code));
        Assert.True(candidate.Digest.SequenceEqual(issued.Digest));
    }

    [Fact]
    public void Active_and_retiring_keys_both_validate_candidates()
    {
        var service = Service(Key("active", 1), Key("retiring", 33), active: "active");
        var issued = service.Issue();

        var candidates = service.CandidateDigests(issued.Code);

        Assert.Equal(["active", "retiring"], candidates.Select(value => value.KeyId));
        Assert.True(candidates[0].Digest.SequenceEqual(issued.Digest));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64url")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=")]
    public void Malformed_codes_return_no_candidates(string code) =>
        Assert.Empty(Service(Key("active", 1), active: "active").CandidateDigests(code));

    [Fact]
    public void Invalid_or_disabled_key_ring_denies_closed()
    {
        Assert.False(Service(Key("active", 1), active: "missing").IsAvailable);
        var options = Options.Create(new AccountInvitationOptions());
        Assert.False(new InvitationTokenService(options).IsAvailable);
    }

    private static AccountInvitationKeyOptions Key(string id, int start) => new()
    {
        Id = id,
        SecretBase64 = Convert.ToBase64String(Enumerable.Range(start, 32)
            .Select(value => (byte)value).ToArray())
    };

    private static InvitationTokenService Service(
        AccountInvitationKeyOptions first,
        AccountInvitationKeyOptions? second = null,
        string active = "active") => new(Options.Create(new AccountInvitationOptions
        {
            Enabled = true,
            LifetimeHours = 168,
            MaximumCodeBytes = 64,
            ActiveKeyId = active,
            Keys = second is null ? [first] : [first, second]
        }));
}
