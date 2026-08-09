using ShowVault.Platform.Agents;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class SubnetProposalTests
{
    [Fact]
    public void Accepts_aligned_bounded_ipv4_link_local_network()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "169.254.0.0", 16,
            "GigabitEthernet", "Bounded direct link", DateTimeOffset.UtcNow);

        Assert.Equal("169.254.0.0", proposal.Network);
        Assert.Equal(16, proposal.PrefixLength);
    }

    [Theory]
    [InlineData("169.253.73.0", 24)]
    [InlineData("169.254.73.1", 24)]
    [InlineData("169.254.73.0", 24)]
    public void Rejects_non_link_local_unaligned_or_overbroad_networks(string network, int prefix)
    {
        Assert.Throws<ArgumentException>(() => SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), network, prefix,
            "Ethernet", "Invalid direct link", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Persists_consistent_path_free_discovery_target_diagnostics()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "169.254.0.0", 16,
            "Ethernet", "Direct link", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());

        proposal.CompleteDiscovery(4, 1, 1, 3, DateTimeOffset.UtcNow);

        Assert.Equal(1, proposal.PassiveCandidateCount);
        Assert.Equal(3, proposal.FallbackTargetCount);
    }

    [Fact]
    public void Rejects_discovery_diagnostics_that_do_not_equal_attempted_targets()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            proposal.CompleteDiscovery(4, 1, 1, 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Tracks_blackmagic_videohub_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);
        proposal.StartBlackmagicVideohubIdentification(Guid.NewGuid());

        proposal.CompleteBlackmagicVideohubIdentification(
            1, 1, "Blackmagic Smart Videohub 16x16", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.BlackmagicVideohubIdentificationStatus);
        Assert.Equal("Blackmagic Smart Videohub 16x16",
            proposal.BlackmagicVideohubIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.BlackmagicVideohubIdentificationStatus);
    }

    [Fact]
    public void Tracks_newtek_tricaster_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartNewTekTriCasterIdentification(Guid.NewGuid());
        proposal.CompleteNewTekTriCasterIdentification(
            1, 1, "NewTek TriCaster TC1", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.NewTekTriCasterIdentificationStatus);
        Assert.Equal("NewTek TriCaster TC1", proposal.NewTekTriCasterIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.NewTekTriCasterIdentificationStatus);
    }

    [Fact]
    public void Tracks_birddog_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartBirdDogIdentification(Guid.NewGuid());
        proposal.CompleteBirdDogIdentification(
            1, 1, "BirdDog P200 (A4/A5)", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed, proposal.BirdDogIdentificationStatus);
        Assert.Equal("BirdDog P200 (A4/A5)", proposal.BirdDogIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.BirdDogIdentificationStatus);
    }

    [Fact]
    public void Tracks_panasonic_camera_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartPanasonicCameraIdentification(Guid.NewGuid());
        proposal.CompletePanasonicCameraIdentification(
            1, 1, "Panasonic AW-UE100", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.PanasonicCameraIdentificationStatus);
        Assert.Equal("Panasonic AW-UE100", proposal.PanasonicCameraIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.PanasonicCameraIdentificationStatus);
    }

    [Fact]
    public void Tracks_sony_camera_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartSonyCameraIdentification(Guid.NewGuid());
        proposal.CompleteSonyCameraIdentification(
            1, 1, "Sony SRG-A40", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.SonyCameraIdentificationStatus);
        Assert.Equal("Sony SRG-A40", proposal.SonyCameraIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.SonyCameraIdentificationStatus);
    }

    [Fact]
    public void Tracks_allen_heath_qu_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartAllenHeathQuIdentification(Guid.NewGuid());
        proposal.CompleteAllenHeathQuIdentification(
            1, 1, "Allen & Heath Qu-16", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.AllenHeathQuIdentificationStatus);
        Assert.Equal("Allen & Heath Qu-16", proposal.AllenHeathQuIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.AllenHeathQuIdentificationStatus);
    }

    [Fact]
    public void Tracks_behringer_wing_identification_independently_and_clears_it_on_rediscovery()
    {
        var proposal = SubnetProposal.Detected(
            Guid.NewGuid(), Guid.NewGuid(), "192.168.1.0", 24,
            "Ethernet", "Private network", DateTimeOffset.UtcNow);
        proposal.RecordDecision(SubnetProposalDecision.Approved, "owner", DateTimeOffset.UtcNow);
        proposal.StartDiscovery(Guid.NewGuid());
        proposal.CompleteDiscovery(4, 1, 0, 4, DateTimeOffset.UtcNow);

        proposal.StartBehringerWingIdentification(Guid.NewGuid());
        proposal.CompleteBehringerWingIdentification(
            1, 1, "Behringer WING", DateTimeOffset.UtcNow);

        Assert.Equal(ProductIdentificationStatus.Completed,
            proposal.BehringerWingIdentificationStatus);
        Assert.Equal("Behringer WING", proposal.BehringerWingIdentifiedProductFamilies);
        proposal.StartDiscovery(Guid.NewGuid());
        Assert.Null(proposal.BehringerWingIdentificationStatus);
    }
}
