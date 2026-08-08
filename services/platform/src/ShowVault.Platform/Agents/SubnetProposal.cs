namespace ShowVault.Platform.Agents;

using System.Net;

public enum SubnetProposalDecision { Pending, Approved, Rejected }
public enum SubnetDiscoveryStatus { Pending, Completed, Failed }

public sealed class SubnetProposal
{
    private SubnetProposal() { }

    private SubnetProposal(Guid id, Guid agentId, string network, int prefixLength,
        string interfaceType, string evidence, DateTimeOffset detectedAt)
    {
        Id = id; AgentId = agentId; Network = network; PrefixLength = prefixLength;
        InterfaceType = interfaceType; Evidence = evidence; DetectedAt = detectedAt;
    }

    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Network { get; private set; } = string.Empty;
    public int PrefixLength { get; private set; }
    public string InterfaceType { get; private set; } = string.Empty;
    public string Evidence { get; private set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; private set; }
    public SubnetProposalDecision Decision { get; private set; }
    public string? DecidedBySubject { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DiscoveryCommandId { get; private set; }
    public SubnetDiscoveryStatus? DiscoveryStatus { get; private set; }
    public int? AttemptedHostCount { get; private set; }
    public int? RespondingHostCount { get; private set; }
    public string? DiscoveryMessage { get; private set; }
    public DateTimeOffset? DiscoveredAt { get; private set; }

    public static SubnetProposal Detected(Guid id, Guid agentId, string network, int prefixLength,
        string interfaceType, string evidence, DateTimeOffset detectedAt)
    {
        if (id == Guid.Empty || agentId == Guid.Empty || prefixLength is < 24 or > 30 ||
            !IPAddress.TryParse(network, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Subnet proposal identity or prefix is invalid.");
        var bytes = address.GetAddressBytes();
        var isPrivate = bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
        var hostMask = uint.MaxValue >> prefixLength;
        var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        if (!isPrivate || (value & hostMask) != 0)
            throw new ArgumentException("Subnet proposal must be an aligned private IPv4 network.");
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return new(id, agentId, network, prefixLength, interfaceType, evidence, detectedAt);
    }

    public void RecordDecision(SubnetProposalDecision decision, string subject, DateTimeOffset at)
    {
        if (decision == SubnetProposalDecision.Pending) throw new ArgumentException("Decision is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Decision = decision; DecidedBySubject = subject; DecidedAt = at;
        DiscoveryCommandId = null; DiscoveryStatus = null; AttemptedHostCount = null;
        RespondingHostCount = null; DiscoveryMessage = null; DiscoveredAt = null;
    }

    public void StartDiscovery(Guid commandId)
    {
        if (Decision != SubnetProposalDecision.Approved || commandId == Guid.Empty)
            throw new InvalidOperationException("Only an approved subnet can be authorized for discovery.");
        DiscoveryCommandId = commandId; DiscoveryStatus = SubnetDiscoveryStatus.Pending;
        AttemptedHostCount = null; RespondingHostCount = null; DiscoveryMessage = null; DiscoveredAt = null;
    }

    public void CompleteDiscovery(int attempted, int responding, DateTimeOffset at)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Pending || attempted is < 0 or > 32 || responding < 0 || responding > attempted)
            throw new InvalidOperationException("Subnet discovery result is invalid.");
        DiscoveryStatus = SubnetDiscoveryStatus.Completed; AttemptedHostCount = attempted;
        RespondingHostCount = responding; DiscoveredAt = at;
    }

    public void FailDiscovery(string message, DateTimeOffset at)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Pending) throw new InvalidOperationException("Subnet discovery is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        DiscoveryStatus = SubnetDiscoveryStatus.Failed; DiscoveryMessage = message; DiscoveredAt = at;
    }
}
