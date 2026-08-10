namespace ShowVault.Api.HostedSync;

public sealed class HostedSyncUnavailableException(string message) : Exception(message);

public sealed class HostedSyncValidationException(string message) : Exception(message);

public sealed class HostedSyncConflictException(string message) : Exception(message);
