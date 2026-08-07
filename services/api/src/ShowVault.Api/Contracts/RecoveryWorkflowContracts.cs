namespace ShowVault.Api.Contracts;

public sealed record VenueAgentSummary(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt);

public sealed record StartRecoveryDiscoveryRequest(
    string PluginId,
    string RootPath,
    int MaxFiles = 1_000);

public sealed record CreateRecoveryBackupRequest(Guid DiscoveryCommandId);

public sealed record VerifyRecoveryBackupRequest(Guid BackupCommandId);

public sealed record StartRecoveryRestoreRequest(
    Guid BackupCommandId,
    Guid VerificationCommandId,
    string TargetPath);
