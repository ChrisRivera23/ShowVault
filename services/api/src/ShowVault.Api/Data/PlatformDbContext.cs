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
    }
}
