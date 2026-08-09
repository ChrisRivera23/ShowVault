namespace ShowVault.Platform.Agents;

using System.Net;

public enum SubnetProposalDecision { Pending, Approved, Rejected }
public enum SubnetDiscoveryStatus { Pending, Completed, Failed }
public enum ProductIdentificationStatus { Pending, Completed, Failed }

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
    public int? PassiveCandidateCount { get; private set; }
    public int? FallbackTargetCount { get; private set; }
    public string? DiscoveryMessage { get; private set; }
    public DateTimeOffset? DiscoveredAt { get; private set; }
    public Guid? IdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? IdentificationStatus { get; private set; }
    public int? IdentificationAttemptedHostCount { get; private set; }
    public int? IdentifiedHostCount { get; private set; }
    public string? IdentifiedProductFamilies { get; private set; }
    public string? IdentificationMessage { get; private set; }
    public DateTimeOffset? IdentifiedAt { get; private set; }
    public Guid? YamahaIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? YamahaIdentificationStatus { get; private set; }
    public int? YamahaIdentificationAttemptedHostCount { get; private set; }
    public int? YamahaIdentifiedHostCount { get; private set; }
    public string? YamahaIdentifiedProductFamilies { get; private set; }
    public string? YamahaIdentificationMessage { get; private set; }
    public DateTimeOffset? YamahaIdentifiedAt { get; private set; }
    public Guid? GrandMa2IdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? GrandMa2IdentificationStatus { get; private set; }
    public int? GrandMa2IdentificationAttemptedHostCount { get; private set; }
    public int? GrandMa2IdentifiedHostCount { get; private set; }
    public string? GrandMa2IdentifiedProductFamilies { get; private set; }
    public string? GrandMa2IdentificationMessage { get; private set; }
    public DateTimeOffset? GrandMa2IdentifiedAt { get; private set; }
    public Guid? BlackmagicVideohubIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? BlackmagicVideohubIdentificationStatus { get; private set; }
    public int? BlackmagicVideohubIdentificationAttemptedHostCount { get; private set; }
    public int? BlackmagicVideohubIdentifiedHostCount { get; private set; }
    public string? BlackmagicVideohubIdentifiedProductFamilies { get; private set; }
    public string? BlackmagicVideohubIdentificationMessage { get; private set; }
    public DateTimeOffset? BlackmagicVideohubIdentifiedAt { get; private set; }
    public Guid? NewTekTriCasterIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? NewTekTriCasterIdentificationStatus { get; private set; }
    public int? NewTekTriCasterIdentificationAttemptedHostCount { get; private set; }
    public int? NewTekTriCasterIdentifiedHostCount { get; private set; }
    public string? NewTekTriCasterIdentifiedProductFamilies { get; private set; }
    public string? NewTekTriCasterIdentificationMessage { get; private set; }
    public DateTimeOffset? NewTekTriCasterIdentifiedAt { get; private set; }
    public Guid? BirdDogIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? BirdDogIdentificationStatus { get; private set; }
    public int? BirdDogIdentificationAttemptedHostCount { get; private set; }
    public int? BirdDogIdentifiedHostCount { get; private set; }
    public string? BirdDogIdentifiedProductFamilies { get; private set; }
    public string? BirdDogIdentificationMessage { get; private set; }
    public DateTimeOffset? BirdDogIdentifiedAt { get; private set; }
    public Guid? PanasonicCameraIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? PanasonicCameraIdentificationStatus { get; private set; }
    public int? PanasonicCameraIdentificationAttemptedHostCount { get; private set; }
    public int? PanasonicCameraIdentifiedHostCount { get; private set; }
    public string? PanasonicCameraIdentifiedProductFamilies { get; private set; }
    public string? PanasonicCameraIdentificationMessage { get; private set; }
    public DateTimeOffset? PanasonicCameraIdentifiedAt { get; private set; }
    public Guid? SonyCameraIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? SonyCameraIdentificationStatus { get; private set; }
    public int? SonyCameraIdentificationAttemptedHostCount { get; private set; }
    public int? SonyCameraIdentifiedHostCount { get; private set; }
    public string? SonyCameraIdentifiedProductFamilies { get; private set; }
    public string? SonyCameraIdentificationMessage { get; private set; }
    public DateTimeOffset? SonyCameraIdentifiedAt { get; private set; }
    public Guid? AllenHeathQuIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? AllenHeathQuIdentificationStatus { get; private set; }
    public int? AllenHeathQuIdentificationAttemptedHostCount { get; private set; }
    public int? AllenHeathQuIdentifiedHostCount { get; private set; }
    public string? AllenHeathQuIdentifiedProductFamilies { get; private set; }
    public string? AllenHeathQuIdentificationMessage { get; private set; }
    public DateTimeOffset? AllenHeathQuIdentifiedAt { get; private set; }
    public Guid? BehringerWingIdentificationCommandId { get; private set; }
    public ProductIdentificationStatus? BehringerWingIdentificationStatus { get; private set; }
    public int? BehringerWingIdentificationAttemptedHostCount { get; private set; }
    public int? BehringerWingIdentifiedHostCount { get; private set; }
    public string? BehringerWingIdentifiedProductFamilies { get; private set; }
    public string? BehringerWingIdentificationMessage { get; private set; }
    public DateTimeOffset? BehringerWingIdentifiedAt { get; private set; }

    public static SubnetProposal Detected(Guid id, Guid agentId, string network, int prefixLength,
        string interfaceType, string evidence, DateTimeOffset detectedAt)
    {
        if (id == Guid.Empty || agentId == Guid.Empty ||
            !IPAddress.TryParse(network, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Subnet proposal identity or prefix is invalid.");
        var bytes = address.GetAddressBytes();
        var isPrivate = bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
        var isLinkLocal = bytes[0] == 169 && bytes[1] == 254;
        var hasSafePrefix = (isPrivate && prefixLength is >= 24 and <= 30) ||
            (isLinkLocal && prefixLength == 16);
        if (!hasSafePrefix)
            throw new ArgumentException("Subnet proposal prefix is outside its safe IPv4 bounds.");
        var hostMask = uint.MaxValue >> prefixLength;
        var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        if ((value & hostMask) != 0)
            throw new ArgumentException("Subnet proposal must be an aligned private or IPv4 link-local network.");
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
        RespondingHostCount = null; PassiveCandidateCount = null; FallbackTargetCount = null;
        DiscoveryMessage = null; DiscoveredAt = null;
        ClearIdentification();
        ClearYamahaIdentification();
        ClearGrandMa2Identification();
        ClearBlackmagicVideohubIdentification();
        ClearNewTekTriCasterIdentification();
        ClearBirdDogIdentification();
        ClearPanasonicCameraIdentification();
        ClearSonyCameraIdentification();
        ClearAllenHeathQuIdentification();
        ClearBehringerWingIdentification();
    }

    public void StartDiscovery(Guid commandId)
    {
        if (Decision != SubnetProposalDecision.Approved || commandId == Guid.Empty)
            throw new InvalidOperationException("Only an approved subnet can be authorized for discovery.");
        DiscoveryCommandId = commandId; DiscoveryStatus = SubnetDiscoveryStatus.Pending;
        AttemptedHostCount = null; RespondingHostCount = null; PassiveCandidateCount = null;
        FallbackTargetCount = null; DiscoveryMessage = null; DiscoveredAt = null;
        ClearIdentification();
        ClearYamahaIdentification();
        ClearGrandMa2Identification();
        ClearBlackmagicVideohubIdentification();
        ClearNewTekTriCasterIdentification();
        ClearBirdDogIdentification();
        ClearPanasonicCameraIdentification();
        ClearSonyCameraIdentification();
        ClearAllenHeathQuIdentification();
        ClearBehringerWingIdentification();
    }

    public void CompleteDiscovery(
        int attempted, int responding, int passiveCandidates, int fallbackTargets, DateTimeOffset at)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Pending || attempted is < 0 or > 32 || responding < 0 || responding > attempted)
            throw new InvalidOperationException("Subnet discovery result is invalid.");
        if (passiveCandidates < 0 || fallbackTargets < 0 || passiveCandidates + fallbackTargets != attempted)
            throw new InvalidOperationException("Subnet discovery target diagnostics are invalid.");
        DiscoveryStatus = SubnetDiscoveryStatus.Completed; AttemptedHostCount = attempted;
        RespondingHostCount = responding; PassiveCandidateCount = passiveCandidates;
        FallbackTargetCount = fallbackTargets; DiscoveredAt = at;
    }

    public void FailDiscovery(string message, DateTimeOffset at)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Pending) throw new InvalidOperationException("Subnet discovery is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        DiscoveryStatus = SubnetDiscoveryStatus.Failed; DiscoveryMessage = message; DiscoveredAt = at;
    }

    public void StartIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException("Product identification requires completed discovery with responders.");
        ClearIdentification();
        IdentificationCommandId = commandId;
        IdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteIdentification(int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (IdentificationStatus != ProductIdentificationStatus.Pending || attempted is < 1 or > 32 ||
            identified < 0 || identified > attempted || string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Product identification result is invalid.");
        IdentificationStatus = ProductIdentificationStatus.Completed;
        IdentificationAttemptedHostCount = attempted;
        IdentifiedHostCount = identified;
        IdentifiedProductFamilies = productFamilies;
        IdentifiedAt = at;
    }

    public void FailIdentification(string message, DateTimeOffset at)
    {
        if (IdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Product identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        IdentificationStatus = ProductIdentificationStatus.Failed;
        IdentificationMessage = message;
        IdentifiedAt = at;
    }

    private void ClearIdentification()
    {
        IdentificationCommandId = null;
        IdentificationStatus = null;
        IdentificationAttemptedHostCount = null;
        IdentifiedHostCount = null;
        IdentifiedProductFamilies = null;
        IdentificationMessage = null;
        IdentifiedAt = null;
    }

    public void StartYamahaIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException("Yamaha identification requires completed discovery with responders.");
        ClearYamahaIdentification();
        YamahaIdentificationCommandId = commandId;
        YamahaIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteYamahaIdentification(int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (YamahaIdentificationStatus != ProductIdentificationStatus.Pending || attempted is < 1 or > 32 ||
            identified < 0 || identified > attempted || string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Yamaha identification result is invalid.");
        YamahaIdentificationStatus = ProductIdentificationStatus.Completed;
        YamahaIdentificationAttemptedHostCount = attempted;
        YamahaIdentifiedHostCount = identified;
        YamahaIdentifiedProductFamilies = productFamilies;
        YamahaIdentifiedAt = at;
    }

    public void FailYamahaIdentification(string message, DateTimeOffset at)
    {
        if (YamahaIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Yamaha identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        YamahaIdentificationStatus = ProductIdentificationStatus.Failed;
        YamahaIdentificationMessage = message;
        YamahaIdentifiedAt = at;
    }

    private void ClearYamahaIdentification()
    {
        YamahaIdentificationCommandId = null;
        YamahaIdentificationStatus = null;
        YamahaIdentificationAttemptedHostCount = null;
        YamahaIdentifiedHostCount = null;
        YamahaIdentifiedProductFamilies = null;
        YamahaIdentificationMessage = null;
        YamahaIdentifiedAt = null;
    }

    public void StartGrandMa2Identification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException("grandMA2 identification requires completed discovery with responders.");
        ClearGrandMa2Identification();
        GrandMa2IdentificationCommandId = commandId;
        GrandMa2IdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteGrandMa2Identification(int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (GrandMa2IdentificationStatus != ProductIdentificationStatus.Pending || attempted is < 1 or > 32 ||
            identified < 0 || identified > attempted || string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("grandMA2 identification result is invalid.");
        GrandMa2IdentificationStatus = ProductIdentificationStatus.Completed;
        GrandMa2IdentificationAttemptedHostCount = attempted;
        GrandMa2IdentifiedHostCount = identified;
        GrandMa2IdentifiedProductFamilies = productFamilies;
        GrandMa2IdentifiedAt = at;
    }

    public void FailGrandMa2Identification(string message, DateTimeOffset at)
    {
        if (GrandMa2IdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("grandMA2 identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        GrandMa2IdentificationStatus = ProductIdentificationStatus.Failed;
        GrandMa2IdentificationMessage = message;
        GrandMa2IdentifiedAt = at;
    }

    private void ClearGrandMa2Identification()
    {
        GrandMa2IdentificationCommandId = null;
        GrandMa2IdentificationStatus = null;
        GrandMa2IdentificationAttemptedHostCount = null;
        GrandMa2IdentifiedHostCount = null;
        GrandMa2IdentifiedProductFamilies = null;
        GrandMa2IdentificationMessage = null;
        GrandMa2IdentifiedAt = null;
    }

    public void StartBlackmagicVideohubIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "Blackmagic Videohub identification requires completed discovery with responders.");
        ClearBlackmagicVideohubIdentification();
        BlackmagicVideohubIdentificationCommandId = commandId;
        BlackmagicVideohubIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteBlackmagicVideohubIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (BlackmagicVideohubIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Blackmagic Videohub identification result is invalid.");
        BlackmagicVideohubIdentificationStatus = ProductIdentificationStatus.Completed;
        BlackmagicVideohubIdentificationAttemptedHostCount = attempted;
        BlackmagicVideohubIdentifiedHostCount = identified;
        BlackmagicVideohubIdentifiedProductFamilies = productFamilies;
        BlackmagicVideohubIdentifiedAt = at;
    }

    public void FailBlackmagicVideohubIdentification(string message, DateTimeOffset at)
    {
        if (BlackmagicVideohubIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Blackmagic Videohub identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        BlackmagicVideohubIdentificationStatus = ProductIdentificationStatus.Failed;
        BlackmagicVideohubIdentificationMessage = message;
        BlackmagicVideohubIdentifiedAt = at;
    }

    private void ClearBlackmagicVideohubIdentification()
    {
        BlackmagicVideohubIdentificationCommandId = null;
        BlackmagicVideohubIdentificationStatus = null;
        BlackmagicVideohubIdentificationAttemptedHostCount = null;
        BlackmagicVideohubIdentifiedHostCount = null;
        BlackmagicVideohubIdentifiedProductFamilies = null;
        BlackmagicVideohubIdentificationMessage = null;
        BlackmagicVideohubIdentifiedAt = null;
    }

    public void StartNewTekTriCasterIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "NewTek TriCaster identification requires completed discovery with responders.");
        ClearNewTekTriCasterIdentification();
        NewTekTriCasterIdentificationCommandId = commandId;
        NewTekTriCasterIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteNewTekTriCasterIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (NewTekTriCasterIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("NewTek TriCaster identification result is invalid.");
        NewTekTriCasterIdentificationStatus = ProductIdentificationStatus.Completed;
        NewTekTriCasterIdentificationAttemptedHostCount = attempted;
        NewTekTriCasterIdentifiedHostCount = identified;
        NewTekTriCasterIdentifiedProductFamilies = productFamilies;
        NewTekTriCasterIdentifiedAt = at;
    }

    public void FailNewTekTriCasterIdentification(string message, DateTimeOffset at)
    {
        if (NewTekTriCasterIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("NewTek TriCaster identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        NewTekTriCasterIdentificationStatus = ProductIdentificationStatus.Failed;
        NewTekTriCasterIdentificationMessage = message;
        NewTekTriCasterIdentifiedAt = at;
    }

    private void ClearNewTekTriCasterIdentification()
    {
        NewTekTriCasterIdentificationCommandId = null;
        NewTekTriCasterIdentificationStatus = null;
        NewTekTriCasterIdentificationAttemptedHostCount = null;
        NewTekTriCasterIdentifiedHostCount = null;
        NewTekTriCasterIdentifiedProductFamilies = null;
        NewTekTriCasterIdentificationMessage = null;
        NewTekTriCasterIdentifiedAt = null;
    }

    public void StartBirdDogIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "BirdDog identification requires completed discovery with responders.");
        ClearBirdDogIdentification();
        BirdDogIdentificationCommandId = commandId;
        BirdDogIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteBirdDogIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (BirdDogIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("BirdDog identification result is invalid.");
        BirdDogIdentificationStatus = ProductIdentificationStatus.Completed;
        BirdDogIdentificationAttemptedHostCount = attempted;
        BirdDogIdentifiedHostCount = identified;
        BirdDogIdentifiedProductFamilies = productFamilies;
        BirdDogIdentifiedAt = at;
    }

    public void FailBirdDogIdentification(string message, DateTimeOffset at)
    {
        if (BirdDogIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("BirdDog identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        BirdDogIdentificationStatus = ProductIdentificationStatus.Failed;
        BirdDogIdentificationMessage = message;
        BirdDogIdentifiedAt = at;
    }

    private void ClearBirdDogIdentification()
    {
        BirdDogIdentificationCommandId = null;
        BirdDogIdentificationStatus = null;
        BirdDogIdentificationAttemptedHostCount = null;
        BirdDogIdentifiedHostCount = null;
        BirdDogIdentifiedProductFamilies = null;
        BirdDogIdentificationMessage = null;
        BirdDogIdentifiedAt = null;
    }

    public void StartPanasonicCameraIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "Panasonic camera identification requires completed discovery with responders.");
        ClearPanasonicCameraIdentification();
        PanasonicCameraIdentificationCommandId = commandId;
        PanasonicCameraIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompletePanasonicCameraIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (PanasonicCameraIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Panasonic camera identification result is invalid.");
        PanasonicCameraIdentificationStatus = ProductIdentificationStatus.Completed;
        PanasonicCameraIdentificationAttemptedHostCount = attempted;
        PanasonicCameraIdentifiedHostCount = identified;
        PanasonicCameraIdentifiedProductFamilies = productFamilies;
        PanasonicCameraIdentifiedAt = at;
    }

    public void FailPanasonicCameraIdentification(string message, DateTimeOffset at)
    {
        if (PanasonicCameraIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Panasonic camera identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        PanasonicCameraIdentificationStatus = ProductIdentificationStatus.Failed;
        PanasonicCameraIdentificationMessage = message;
        PanasonicCameraIdentifiedAt = at;
    }

    private void ClearPanasonicCameraIdentification()
    {
        PanasonicCameraIdentificationCommandId = null;
        PanasonicCameraIdentificationStatus = null;
        PanasonicCameraIdentificationAttemptedHostCount = null;
        PanasonicCameraIdentifiedHostCount = null;
        PanasonicCameraIdentifiedProductFamilies = null;
        PanasonicCameraIdentificationMessage = null;
        PanasonicCameraIdentifiedAt = null;
    }

    public void StartSonyCameraIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "Sony camera identification requires completed discovery with responders.");
        ClearSonyCameraIdentification();
        SonyCameraIdentificationCommandId = commandId;
        SonyCameraIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteSonyCameraIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (SonyCameraIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Sony camera identification result is invalid.");
        SonyCameraIdentificationStatus = ProductIdentificationStatus.Completed;
        SonyCameraIdentificationAttemptedHostCount = attempted;
        SonyCameraIdentifiedHostCount = identified;
        SonyCameraIdentifiedProductFamilies = productFamilies;
        SonyCameraIdentifiedAt = at;
    }

    public void FailSonyCameraIdentification(string message, DateTimeOffset at)
    {
        if (SonyCameraIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Sony camera identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        SonyCameraIdentificationStatus = ProductIdentificationStatus.Failed;
        SonyCameraIdentificationMessage = message;
        SonyCameraIdentifiedAt = at;
    }

    private void ClearSonyCameraIdentification()
    {
        SonyCameraIdentificationCommandId = null;
        SonyCameraIdentificationStatus = null;
        SonyCameraIdentificationAttemptedHostCount = null;
        SonyCameraIdentifiedHostCount = null;
        SonyCameraIdentifiedProductFamilies = null;
        SonyCameraIdentificationMessage = null;
        SonyCameraIdentifiedAt = null;
    }

    public void StartAllenHeathQuIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "Allen & Heath Qu identification requires completed discovery with responders.");
        ClearAllenHeathQuIdentification();
        AllenHeathQuIdentificationCommandId = commandId;
        AllenHeathQuIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteAllenHeathQuIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (AllenHeathQuIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Allen & Heath Qu identification result is invalid.");
        AllenHeathQuIdentificationStatus = ProductIdentificationStatus.Completed;
        AllenHeathQuIdentificationAttemptedHostCount = attempted;
        AllenHeathQuIdentifiedHostCount = identified;
        AllenHeathQuIdentifiedProductFamilies = productFamilies;
        AllenHeathQuIdentifiedAt = at;
    }

    public void FailAllenHeathQuIdentification(string message, DateTimeOffset at)
    {
        if (AllenHeathQuIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Allen & Heath Qu identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        AllenHeathQuIdentificationStatus = ProductIdentificationStatus.Failed;
        AllenHeathQuIdentificationMessage = message;
        AllenHeathQuIdentifiedAt = at;
    }

    private void ClearAllenHeathQuIdentification()
    {
        AllenHeathQuIdentificationCommandId = null;
        AllenHeathQuIdentificationStatus = null;
        AllenHeathQuIdentificationAttemptedHostCount = null;
        AllenHeathQuIdentifiedHostCount = null;
        AllenHeathQuIdentifiedProductFamilies = null;
        AllenHeathQuIdentificationMessage = null;
        AllenHeathQuIdentifiedAt = null;
    }

    public void StartBehringerWingIdentification(Guid commandId)
    {
        if (DiscoveryStatus != SubnetDiscoveryStatus.Completed || RespondingHostCount is null or <= 0 ||
            commandId == Guid.Empty)
            throw new InvalidOperationException(
                "Behringer WING identification requires completed discovery with responders.");
        ClearBehringerWingIdentification();
        BehringerWingIdentificationCommandId = commandId;
        BehringerWingIdentificationStatus = ProductIdentificationStatus.Pending;
    }

    public void CompleteBehringerWingIdentification(
        int attempted, int identified, string productFamilies, DateTimeOffset at)
    {
        if (BehringerWingIdentificationStatus != ProductIdentificationStatus.Pending ||
            attempted is < 1 or > 32 || identified < 0 || identified > attempted ||
            string.IsNullOrWhiteSpace(productFamilies))
            throw new InvalidOperationException("Behringer WING identification result is invalid.");
        BehringerWingIdentificationStatus = ProductIdentificationStatus.Completed;
        BehringerWingIdentificationAttemptedHostCount = attempted;
        BehringerWingIdentifiedHostCount = identified;
        BehringerWingIdentifiedProductFamilies = productFamilies;
        BehringerWingIdentifiedAt = at;
    }

    public void FailBehringerWingIdentification(string message, DateTimeOffset at)
    {
        if (BehringerWingIdentificationStatus != ProductIdentificationStatus.Pending)
            throw new InvalidOperationException("Behringer WING identification is not pending.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        BehringerWingIdentificationStatus = ProductIdentificationStatus.Failed;
        BehringerWingIdentificationMessage = message;
        BehringerWingIdentifiedAt = at;
    }

    private void ClearBehringerWingIdentification()
    {
        BehringerWingIdentificationCommandId = null;
        BehringerWingIdentificationStatus = null;
        BehringerWingIdentificationAttemptedHostCount = null;
        BehringerWingIdentifiedHostCount = null;
        BehringerWingIdentifiedProductFamilies = null;
        BehringerWingIdentificationMessage = null;
        BehringerWingIdentifiedAt = null;
    }
}
