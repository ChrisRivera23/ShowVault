using Microsoft.EntityFrameworkCore;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;

namespace ShowVault.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<AgentEnrollment> AgentEnrollments => Set<AgentEnrollment>();
    public DbSet<VenueAgent> VenueAgents => Set<VenueAgent>();
    public DbSet<ReceivedAgentEvent> ReceivedAgentEvents => Set<ReceivedAgentEvent>();
    public DbSet<IssuedAgentCommand> IssuedAgentCommands => Set<IssuedAgentCommand>();
    public DbSet<RecoveryCandidate> RecoveryCandidates => Set<RecoveryCandidate>();
    public DbSet<SubnetProposal> SubnetProposals => Set<SubnetProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
            entity.Property(organization => organization.Slug).HasMaxLength(80).IsRequired();
            entity.Property(organization => organization.CreatedAt).IsRequired();
            entity.HasIndex(organization => organization.Slug).IsUnique();
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.IdentitySubject).HasMaxLength(255).IsRequired();
            entity.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(membership => membership.CreatedAt).IsRequired();
            entity.HasIndex(membership => new
            {
                membership.OrganizationId,
                membership.IdentitySubject
            }).IsUnique();
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(membership => membership.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(venue => venue.Id);
            entity.Property(venue => venue.Name).HasMaxLength(200).IsRequired();
            entity.Property(venue => venue.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(venue => venue.CreatedAt).IsRequired();
            entity.HasIndex(venue => new { venue.OrganizationId, venue.Name });
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(venue => venue.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentEnrollment>(entity =>
        {
            entity.ToTable("agent_enrollments");
            entity.HasKey(enrollment => enrollment.Id);
            entity.Property(enrollment => enrollment.SecretHash).HasMaxLength(32).IsRequired();
            entity.Property(enrollment => enrollment.CreatedBySubject).HasMaxLength(255).IsRequired();
            entity.Property(enrollment => enrollment.CreatedAt).IsRequired();
            entity.Property(enrollment => enrollment.ExpiresAt).IsRequired();
            entity.Property(enrollment => enrollment.ConsumedAt).IsConcurrencyToken();
            entity.Property(enrollment => enrollment.RevokedAt);
            entity.HasIndex(enrollment => enrollment.SecretHash).IsUnique();
            entity.HasIndex(enrollment => enrollment.VenueId);
            entity.HasOne<Venue>()
                .WithMany()
                .HasForeignKey(enrollment => enrollment.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VenueAgent>(entity =>
        {
            entity.ToTable("venue_agents");
            entity.HasKey(agent => agent.Id);
            entity.Property(agent => agent.Name).HasMaxLength(200).IsRequired();
            entity.Property(agent => agent.CredentialHash)
                .HasMaxLength(32)
                .IsRequired()
                .IsConcurrencyToken();
            entity.Property(agent => agent.CreatedAt).IsRequired();
            entity.Property(agent => agent.CredentialRotatedAt).IsRequired();
            entity.Property(agent => agent.RevokedAt);
            entity.HasIndex(agent => agent.VenueId);
            entity.HasOne<Venue>()
                .WithMany()
                .HasForeignKey(agent => agent.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceivedAgentEvent>(entity =>
        {
            entity.ToTable("received_agent_events");
            entity.HasKey(agentEvent => agentEvent.EventId);
            entity.Property(agentEvent => agentEvent.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(agentEvent => agentEvent.ProtocolVersion).HasMaxLength(20).IsRequired();
            entity.Property(agentEvent => agentEvent.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(agentEvent => agentEvent.Payload).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(agentEvent => new { agentEvent.AgentId, agentEvent.ReceivedAt });
            entity.HasOne<VenueAgent>()
                .WithMany()
                .HasForeignKey(agentEvent => agentEvent.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IssuedAgentCommand>(entity =>
        {
            entity.ToTable("issued_agent_commands");
            entity.HasKey(command => command.CommandId);
            entity.Property(command => command.AgentId).IsRequired();
            entity.Property(command => command.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(command => command.ProtocolVersion).HasMaxLength(20).IsRequired();
            entity.Property(command => command.IssuedAt).IsRequired();
            entity.Property(command => command.ExpiresAt).IsRequired();
            entity.Property(command => command.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(command => command.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(command => command.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(command => command.Status).IsConcurrencyToken();
            entity.HasIndex(command => new { command.AgentId, command.Status, command.IssuedAt });
            entity.HasOne<VenueAgent>()
                .WithMany()
                .HasForeignKey(command => command.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecoveryCandidate>(entity =>
        {
            entity.ToTable("recovery_candidates");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.PluginId).HasMaxLength(200).IsRequired();
            entity.Property(candidate => candidate.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(candidate => candidate.CandidateType).HasMaxLength(80).IsRequired();
            entity.Property(candidate => candidate.Evidence).HasMaxLength(500).IsRequired();
            entity.Property(candidate => candidate.Decision).HasConversion<string>().HasMaxLength(32);
            entity.Property(candidate => candidate.ValidationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(candidate => candidate.ValidationMessage).HasMaxLength(500);
            entity.Property(candidate => candidate.DecidedBySubject).HasMaxLength(255);
            entity.HasIndex(candidate => new { candidate.AgentId, candidate.DetectedAt });
            entity.HasIndex(candidate => candidate.ValidationCommandId).IsUnique();
            entity.HasOne<VenueAgent>()
                .WithMany()
                .HasForeignKey(candidate => candidate.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SubnetProposal>(entity =>
        {
            entity.ToTable("subnet_proposals");
            entity.HasKey(proposal => proposal.Id);
            entity.Property(proposal => proposal.Network).HasMaxLength(15).IsRequired();
            entity.Property(proposal => proposal.InterfaceType).HasMaxLength(40).IsRequired();
            entity.Property(proposal => proposal.Evidence).HasMaxLength(500).IsRequired();
            entity.Property(proposal => proposal.Decision).HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.DiscoveryStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.DiscoveryMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.IdentificationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.IdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.IdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.YamahaIdentificationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.YamahaIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.YamahaIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.GrandMa2IdentificationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.GrandMa2IdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.GrandMa2IdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.BlackmagicVideohubIdentificationStatus)
                .HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.BlackmagicVideohubIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.BlackmagicVideohubIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.NewTekTriCasterIdentificationStatus)
                .HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.NewTekTriCasterIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.NewTekTriCasterIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.BirdDogIdentificationStatus)
                .HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.BirdDogIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.BirdDogIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.PanasonicCameraIdentificationStatus)
                .HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.PanasonicCameraIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.PanasonicCameraIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.SonyCameraIdentificationStatus)
                .HasConversion<string>().HasMaxLength(32);
            entity.Property(proposal => proposal.SonyCameraIdentifiedProductFamilies).HasMaxLength(200);
            entity.Property(proposal => proposal.SonyCameraIdentificationMessage).HasMaxLength(500);
            entity.Property(proposal => proposal.DecidedBySubject).HasMaxLength(255);
            entity.HasIndex(proposal => new { proposal.AgentId, proposal.DetectedAt });
            entity.HasIndex(proposal => proposal.DiscoveryCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.IdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.YamahaIdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.GrandMa2IdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.BlackmagicVideohubIdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.NewTekTriCasterIdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.BirdDogIdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.PanasonicCameraIdentificationCommandId).IsUnique();
            entity.HasIndex(proposal => proposal.SonyCameraIdentificationCommandId).IsUnique();
            entity.HasOne<VenueAgent>().WithMany().HasForeignKey(proposal => proposal.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
