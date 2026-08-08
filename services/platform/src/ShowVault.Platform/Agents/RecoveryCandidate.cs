namespace ShowVault.Platform.Agents;

public enum RecoveryCandidateDecision
{
    Pending,
    Approved,
    Rejected
}

public enum RecoveryCandidateValidationStatus
{
    Pending,
    Passed,
    Failed
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
    public Guid? ValidationCommandId { get; private set; }
    public RecoveryCandidateValidationStatus? ValidationStatus { get; private set; }
    public int? ValidationFileCount { get; private set; }
    public bool? ValidationTruncated { get; private set; }
    public string? ValidationMessage { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }

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
        ValidationCommandId = null;
        ValidationStatus = null;
        ValidationFileCount = null;
        ValidationTruncated = null;
        ValidationMessage = null;
        ValidatedAt = null;
    }

    public void StartValidation(Guid commandId)
    {
        if (Decision != RecoveryCandidateDecision.Approved || commandId == Guid.Empty)
        {
            throw new InvalidOperationException("Only an approved candidate can be validated.");
        }

        ValidationCommandId = commandId;
        ValidationStatus = RecoveryCandidateValidationStatus.Pending;
        ValidationFileCount = null;
        ValidationTruncated = null;
        ValidationMessage = null;
        ValidatedAt = null;
    }

    public void CompleteValidation(int fileCount, bool truncated, DateTimeOffset validatedAt)
    {
        if (ValidationStatus != RecoveryCandidateValidationStatus.Pending || fileCount < 0)
        {
            throw new InvalidOperationException("Candidate validation is not pending.");
        }

        ValidationStatus = RecoveryCandidateValidationStatus.Passed;
        ValidationFileCount = fileCount;
        ValidationTruncated = truncated;
        ValidatedAt = validatedAt;
    }

    public void FailValidation(string message, DateTimeOffset validatedAt)
    {
        if (ValidationStatus != RecoveryCandidateValidationStatus.Pending)
        {
            throw new InvalidOperationException("Candidate validation is not pending.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ValidationStatus = RecoveryCandidateValidationStatus.Failed;
        ValidationMessage = message;
        ValidatedAt = validatedAt;
    }
}
