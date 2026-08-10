using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public sealed partial class HostedSyncOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<HostedSyncOptions>
{
    public ValidateOptionsResult Validate(string? name, HostedSyncOptions options)
    {
        if (options.Provider == HostedSyncProviders.Disabled)
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    "HostedSync:Provider must be configured outside Development.");
        }

        if (options.Provider == HostedSyncProviders.FileSystem)
        {
            if (!environment.IsDevelopment())
            {
                return ValidateOptionsResult.Fail(
                    "The hosted filesystem provider is restricted to Development.");
            }
            return string.IsNullOrWhiteSpace(options.RootPath)
                ? ValidateOptionsResult.Fail(
                    "HostedSync:RootPath is required for the filesystem provider.")
                : ValidateOptionsResult.Success;
        }

        if (options.Provider != HostedSyncProviders.S3)
        {
            return ValidateOptionsResult.Fail("HostedSync:Provider is unsupported.");
        }

        var s3 = options.S3;
        if (!BucketRegex().IsMatch(s3.Bucket))
        {
            return ValidateOptionsResult.Fail("HostedSync:S3:Bucket is invalid.");
        }
        if (string.IsNullOrWhiteSpace(s3.Region) || s3.Region.Length > 100)
        {
            return ValidateOptionsResult.Fail("HostedSync:S3:Region is required.");
        }
        if (!PrefixRegex().IsMatch(s3.Prefix) ||
            s3.Prefix.Split('/').Any(segment => segment is "." or ".."))
        {
            return ValidateOptionsResult.Fail("HostedSync:S3:Prefix is invalid.");
        }
        if (!string.IsNullOrWhiteSpace(s3.ServiceUrl))
        {
            if (!Uri.TryCreate(s3.ServiceUrl, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps &&
                 !(environment.IsDevelopment() && endpoint.Scheme == Uri.UriSchemeHttp)) ||
                !string.IsNullOrEmpty(endpoint.UserInfo) ||
                endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
            {
                return ValidateOptionsResult.Fail(
                    "HostedSync:S3:ServiceUrl must be an HTTPS endpoint; Development may use HTTP.");
            }
        }
        return ValidateOptionsResult.Success;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]{0,511}$", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixRegex();
}
