namespace ShowVault.Platform.Agents;

public enum RecoveryCandidateDecision
{
    Pending,
    Approved,
    Rejected
}

public sealed class RecoveryCandidate
{
    private RecoveryCandidate(
        Guid id,
        Guid agentId,
        string pluginId,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        Id = id;
        AgentId = agentId;
        PluginId = pluginId;
        ProductName = productName;
        CandidateType = candidateType;
        Evidence = evidence;
        DetectedAt = detectedAt;
        Decision = RecoveryCandidateDecision.Pending;
    }

    public Guid Id { get; }
    public Guid AgentId { get; }
    public string PluginId { get; }
    public string ProductName { get; }
    public string CandidateType { get; }
    public string Evidence { get; }
    public DateTimeOffset DetectedAt { get; }
    public RecoveryCandidateDecision Decision { get; private set; }
    public string? DecidedBySubject { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    public static RecoveryCandidate Detected(
        Guid id,
        Guid agentId,
        string pluginId,
        string productName,
        string candidateType,
        string evidence,
        DateTimeOffset detectedAt)
    {
        if (id == Guid.Empty || agentId == Guid.Empty)
        {
            throw new ArgumentException("Candidate and Agent IDs must not be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return new(id, agentId, pluginId, productName, candidateType, evidence, detectedAt);
    }

    public void RecordDecision(
        RecoveryCandidateDecision decision,
        string subject,
        DateTimeOffset decidedAt)
    {
        if (decision == RecoveryCandidateDecision.Pending)
        {
            throw new ArgumentException("A recorded decision must be approved or rejected.", nameof(decision));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Decision = decision;
        DecidedBySubject = subject;
        DecidedAt = decidedAt;
    }
}
