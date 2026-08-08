namespace ShowVault.AgentContracts;

public static class AgentProtocol
{
    public const string Version = "1.4";
}

public enum AgentCommandType
{
    StartDiscovery,
    CollectSystemInventory,
    DiscoverNetworkDevices,
    ApplyRecoveryCandidateDecision,
    ValidateRecoveryCandidate,
    CreateBackup,
    VerifyBackup,
    GenerateRecoveryPlan,
    StartRestore,
    CancelJob
}

public enum AgentEventType
{
    AgentConnected,
    AgentDisconnected,
    JobAccepted,
    JobProgressed,
    JobCompleted,
    JobFailed
}

public sealed record AgentProtocolDescription(
    string Version,
    IReadOnlyCollection<AgentCommandType> Commands,
    IReadOnlyCollection<AgentEventType> Events)
{
    public static AgentProtocolDescription Current { get; } = new(
        AgentProtocol.Version,
        Enum.GetValues<AgentCommandType>(),
        Enum.GetValues<AgentEventType>());
}
