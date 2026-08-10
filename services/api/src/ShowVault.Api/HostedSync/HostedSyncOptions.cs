namespace ShowVault.Api.HostedSync;

public sealed class HostedSyncOptions
{
    public string Provider { get; set; } = HostedSyncProviders.Disabled;
    public string RootPath { get; set; } = string.Empty;
    public S3HostedSyncOptions S3 { get; set; } = new();
}

public sealed class S3HostedSyncOptions
{
    public string Bucket { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string Prefix { get; set; } = "showvault/v1";
    public bool ForcePathStyle { get; set; }
}

public static class HostedSyncProviders
{
    public const string Disabled = "Disabled";
    public const string FileSystem = "FileSystem";
    public const string S3 = "S3";
}
