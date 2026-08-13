namespace ShowVault.LocalEngine;

public sealed record LocalSaveRequest(
    string CandidateKey,
    string SelectedSource,
    string SelectedVault);

public sealed record LocalSaveResult(
    string RecoveryPointId,
    string ProductName,
    int FileCount,
    long TotalBytes,
    string LocalStatus,
    string CloudStatus);

public sealed record LocalSaveProgress(
    string Stage,
    int CompletedUnits,
    int TotalUnits);

public sealed record LocalRestoreRequest(
    string RecoveryPointId,
    string SelectedVault,
    string SelectedTarget);

public sealed record LocalRestoreResult(
    string RecoveryPointId,
    string RestoreEvidenceId,
    int FileCount,
    long TotalBytes,
    DateTimeOffset CompletedAt,
    string LocalStatus);

public sealed record LocalRestoreProgress(
    string Stage,
    int CompletedUnits,
    int TotalUnits);

public sealed record LocalSyncRequest(
    string SelectedVault,
    Guid OrganizationId,
    Guid VenueId,
    string AccessToken,
    Uri ApiBaseUri,
    int MaximumRecoveryPoints = 25);

public sealed record LocalSyncResult(
    int SynchronizedCount,
    int RetryScheduledCount,
    int AttentionCount,
    long SynchronizedBytes,
    string CloudStatus);

public sealed record LocalSyncProgress(
    string Stage,
    int CompletedUnits,
    int TotalUnits);

public sealed record LocalRecoveryPointSummary(
    string RecoveryPointId,
    string CandidateKey,
    string ProductName,
    int FileCount,
    long TotalBytes,
    DateTimeOffset CreatedAt,
    string LocalStatus,
    string CloudStatus);

public sealed record LocalVaultInspection(
    IReadOnlyList<LocalRecoveryPointSummary> RecoveryPoints,
    int QueueAttentionCount,
    int RestoreAttentionCount);

public sealed record LocalEngineLimits(
    int MaximumFileCount = 10_000,
    int MaximumDirectoryCount = 2_000,
    int MaximumRelativePathLength = 1_024,
    long MaximumFileBytes = 512L * 1024 * 1024,
    long MaximumTotalBytes = 5L * 1024 * 1024 * 1024,
    int MaximumRecoveryPointCount = 10_000,
    TimeSpan? Timeout = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromMinutes(10);
}

public sealed class LocalEngineException(string message) : Exception(message);
