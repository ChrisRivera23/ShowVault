using Microsoft.EntityFrameworkCore;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Commercial;
using ShowVault.Platform.Billing;
using ShowVault.Platform.Support;

namespace ShowVault.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<AccountAuditEvent> AccountAuditEvents => Set<AccountAuditEvent>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<AgentEnrollment> AgentEnrollments => Set<AgentEnrollment>();
    public DbSet<VenueAgent> VenueAgents => Set<VenueAgent>();
    public DbSet<ReceivedAgentEvent> ReceivedAgentEvents => Set<ReceivedAgentEvent>();
    public DbSet<IssuedAgentCommand> IssuedAgentCommands => Set<IssuedAgentCommand>();
    public DbSet<DesktopCatalogScan> DesktopCatalogScans => Set<DesktopCatalogScan>();
    public DbSet<DesktopCatalogScanCandidate> DesktopCatalogScanCandidates =>
        Set<DesktopCatalogScanCandidate>();
    public DbSet<HostedSyncSession> HostedSyncSessions => Set<HostedSyncSession>();
    public DbSet<CommercialLicense> CommercialLicenses => Set<CommercialLicense>();
    public DbSet<ServiceSubscription> ServiceSubscriptions => Set<ServiceSubscription>();
    public DbSet<OrganizationStorageUsage> OrganizationStorageUsages =>
        Set<OrganizationStorageUsage>();
    public DbSet<HostedSyncReservation> HostedSyncReservations => Set<HostedSyncReservation>();
    public DbSet<CommercialAuditEvent> CommercialAuditEvents => Set<CommercialAuditEvent>();
    public DbSet<BillingAccountBinding> BillingAccountBindings => Set<BillingAccountBinding>();
    public DbSet<BillingPurchaseAttempt> BillingPurchaseAttempts => Set<BillingPurchaseAttempt>();
    public DbSet<BillingEventReceipt> BillingEventReceipts => Set<BillingEventReceipt>();
    public DbSet<BillingAttention> BillingAttentions => Set<BillingAttention>();
    public DbSet<SupportStaffAssignment> SupportStaffAssignments => Set<SupportStaffAssignment>();
    public DbSet<SupportOrganizationGrant> SupportOrganizationGrants => Set<SupportOrganizationGrant>();
    public DbSet<SupportAuditEvent> SupportAuditEvents => Set<SupportAuditEvent>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

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
            entity.ToTable("memberships", table => table.HasCheckConstraint(
                "CK_memberships_state", "\"State\" IN ('Active', 'Suspended', 'Revoked')"));
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.IdentitySubject).HasMaxLength(255).IsRequired();
            entity.Property(membership => membership.DisplayLabel).HasMaxLength(80);
            entity.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(membership => membership.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(membership => membership.CreatedAt).IsRequired();
            entity.Property(membership => membership.UpdatedAt).IsRequired();
            entity.Property(membership => membership.Revision).IsConcurrencyToken();
            entity.HasIndex(membership => new
            {
                membership.OrganizationId,
                membership.IdentitySubject
            }).IsUnique();
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(membership => membership.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrganizationInvitation>(entity =>
        {
            entity.ToTable("organization_invitations", table =>
            {
                table.HasCheckConstraint("CK_organization_invitations_state",
                    "\"State\" IN ('Pending', 'Accepted', 'Revoked', 'Expired')");
                table.HasCheckConstraint("CK_organization_invitations_role",
                    "\"Role\" IN ('Viewer', 'Technician', 'Manager', 'Administrator')");
            });
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.DisplayLabel).HasMaxLength(80).IsRequired();
            entity.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(invitation => invitation.TokenDigest).HasMaxLength(32).IsRequired();
            entity.Property(invitation => invitation.TokenKeyId).HasMaxLength(80).IsRequired();
            entity.Property(invitation => invitation.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(invitation => invitation.CreatedBySubject).HasMaxLength(255).IsRequired();
            entity.Property(invitation => invitation.AcceptedBySubject).HasMaxLength(255);
            entity.Property(invitation => invitation.Revision).IsConcurrencyToken();
            entity.HasIndex(invitation => invitation.TokenDigest).IsUnique();
            entity.HasIndex(invitation => new
            {
                invitation.OrganizationId,
                invitation.State,
                invitation.ExpiresAt
            });
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(invitation => invitation.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Membership>().WithMany()
                .HasForeignKey(invitation => invitation.AcceptedMembershipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountAuditEvent>(entity =>
        {
            entity.ToTable("account_audit_events");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.ActorSubject).HasMaxLength(255).IsRequired();
            entity.Property(audit => audit.TargetEntityType).HasMaxLength(40).IsRequired();
            entity.Property(audit => audit.Action).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(audit => audit.ReasonCode).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.PolicyVersion).HasMaxLength(40).IsRequired();
            entity.HasIndex(audit => new { audit.OrganizationId, audit.Action, audit.OccurredAt });
            entity.HasIndex(audit => new
            {
                audit.OrganizationId,
                audit.TargetEntityType,
                audit.TargetEntityId
            });
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(audit => audit.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportStaffAssignment>(entity =>
        {
            entity.ToTable("support_staff_assignments", table =>
            {
                table.HasCheckConstraint("CK_support_staff_assignments_role",
                    "\"Role\" = 'SupportReader'");
                table.HasCheckConstraint("CK_support_staff_assignments_state",
                    "\"State\" IN ('Active', 'Suspended', 'Revoked')");
            });
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.IdentityIssuer).HasMaxLength(255).IsRequired();
            entity.Property(assignment => assignment.IdentitySubject).HasMaxLength(255).IsRequired();
            entity.Property(assignment => assignment.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(assignment => assignment.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(assignment => assignment.CreatedAt).IsRequired();
            entity.Property(assignment => assignment.UpdatedAt).IsRequired();
            entity.Property(assignment => assignment.Revision).IsConcurrencyToken();
            entity.HasIndex(assignment => new
            {
                assignment.IdentityIssuer,
                assignment.IdentitySubject
            }).IsUnique();
        });

        modelBuilder.Entity<SupportOrganizationGrant>(entity =>
        {
            entity.ToTable("support_organization_grants", table =>
                table.HasCheckConstraint("CK_support_organization_grants_state",
                    "\"State\" IN ('Active', 'Revoked')"));
            entity.HasKey(grant => grant.Id);
            entity.Property(grant => grant.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(grant => grant.CreatedAt).IsRequired();
            entity.Property(grant => grant.UpdatedAt).IsRequired();
            entity.Property(grant => grant.Revision).IsConcurrencyToken();
            entity.HasIndex(grant => new
            {
                grant.StaffAssignmentId,
                grant.OrganizationId
            }).IsUnique();
            entity.HasOne<SupportStaffAssignment>().WithMany()
                .HasForeignKey(grant => grant.StaffAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(grant => grant.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportAuditEvent>(entity =>
        {
            entity.ToTable("support_audit_events");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.ActorIssuer).HasMaxLength(255).IsRequired();
            entity.Property(audit => audit.ActorSubject).HasMaxLength(255).IsRequired();
            entity.Property(audit => audit.Action).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(audit => audit.ReasonCode).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.PolicyVersion).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.OccurredAt).IsRequired();
            entity.HasIndex(audit => audit.OccurredAt);
            entity.HasIndex(audit => new { audit.OrganizationId, audit.OccurredAt });
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(audit => audit.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(enrollment => enrollment.ActivationRequestId);
            entity.Property(enrollment => enrollment.IssuedAgentId);
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
            entity.Property(agent => agent.LastCredentialRotationRequestId);
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

        modelBuilder.Entity<DesktopCatalogScan>(entity =>
        {
            entity.ToTable("desktop_catalog_scans");
            entity.HasKey(scan => scan.Id);
            entity.Property(scan => scan.CompletedAt).IsRequired();
            entity.HasIndex(scan => new { scan.VenueId, scan.CompletedAt });
            entity.HasOne<Venue>().WithMany().HasForeignKey(scan => scan.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DesktopCatalogScanCandidate>(entity =>
        {
            entity.ToTable("desktop_catalog_scan_candidates");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.CandidateKey).HasMaxLength(120).IsRequired();
            entity.Property(candidate => candidate.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(candidate => candidate.CandidateType).HasMaxLength(80).IsRequired();
            entity.Property(candidate => candidate.Evidence).HasMaxLength(500).IsRequired();
            entity.Property(candidate => candidate.DetectedAt).IsRequired();
            entity.HasIndex(candidate => new { candidate.ScanId, candidate.CandidateKey }).IsUnique();
            entity.HasOne<DesktopCatalogScan>().WithMany().HasForeignKey(candidate => candidate.ScanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Venue>().WithMany().HasForeignKey(candidate => candidate.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HostedSyncSession>(entity =>
        {
            entity.ToTable("hosted_sync_sessions", table => table.HasCheckConstraint(
                "CK_hosted_sync_sessions_manifest_total_bytes",
                "\"ManifestTotalBytes\" >= 0"));
            entity.HasKey(session => session.Id);
            entity.Property(session => session.RecoveryPointId).HasMaxLength(64).IsRequired();
            entity.Property(session => session.ManifestDigest).HasMaxLength(64).IsRequired();
            entity.Property(session => session.ManifestJson).HasColumnType("jsonb").IsRequired();
            entity.Property(session => session.ManifestTotalBytes).IsRequired();
            entity.Property(session => session.Status).HasMaxLength(32).IsRequired();
            entity.Property(session => session.ReceiptJson).HasColumnType("jsonb");
            entity.Property(session => session.Revision).IsConcurrencyToken();
            entity.HasIndex(session => new
            {
                session.OrganizationId,
                session.VenueId,
                session.RecoveryPointId
            }).IsUnique();
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(session => session.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Venue>().WithMany()
                .HasForeignKey(session => session.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommercialLicense>(entity =>
        {
            entity.ToTable("commercial_licenses");
            entity.HasKey(license => license.Id);
            entity.Property(license => license.LicenseTypeCode).HasMaxLength(80).IsRequired();
            entity.Property(license => license.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(license => license.Revision).IsConcurrencyToken();
            entity.HasIndex(license => license.OrganizationId).IsUnique();
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(license => license.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceSubscription>(entity =>
        {
            entity.ToTable("service_subscriptions");
            entity.HasKey(subscription => subscription.Id);
            entity.Property(subscription => subscription.PlanCode).HasMaxLength(80).IsRequired();
            entity.Property(subscription => subscription.State).HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(subscription => subscription.Revision).IsConcurrencyToken();
            entity.HasIndex(subscription => subscription.OrganizationId).IsUnique();
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(subscription => subscription.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrganizationStorageUsage>(entity =>
        {
            entity.ToTable("organization_storage_usage", table =>
            {
                table.HasCheckConstraint("CK_organization_storage_usage_committed_bytes",
                    "\"CommittedBytes\" >= 0");
                table.HasCheckConstraint("CK_organization_storage_usage_reserved_bytes",
                    "\"ReservedBytes\" >= 0");
            });
            entity.HasKey(usage => usage.OrganizationId);
            entity.Property(usage => usage.Revision).IsConcurrencyToken();
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(usage => usage.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HostedSyncReservation>(entity =>
        {
            entity.ToTable("hosted_sync_reservations", table => table.HasCheckConstraint(
                "CK_hosted_sync_reservations_logical_bytes", "\"LogicalBytes\" >= 0"));
            entity.HasKey(reservation => reservation.HostedSyncSessionId);
            entity.Property(reservation => reservation.State).HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(reservation => reservation.Revision).IsConcurrencyToken();
            entity.HasIndex(reservation => reservation.OrganizationId);
            entity.HasOne<HostedSyncSession>().WithOne()
                .HasForeignKey<HostedSyncReservation>(reservation =>
                    reservation.HostedSyncSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(reservation => reservation.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommercialAuditEvent>(entity =>
        {
            entity.ToTable("commercial_audit_events");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.ActorSubject).HasMaxLength(255);
            entity.Property(audit => audit.Action).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(audit => audit.ReasonCode).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.PolicyVersion).HasMaxLength(80).IsRequired();
            entity.HasIndex(audit => new { audit.OrganizationId, audit.OccurredAt });
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(audit => audit.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingAccountBinding>(entity =>
        {
            entity.ToTable("billing_account_bindings");
            entity.HasKey(binding => binding.OrganizationId);
            entity.Property(binding => binding.Provider).HasMaxLength(32).IsRequired();
            entity.Property(binding => binding.Environment).HasConversion<string>().HasMaxLength(16);
            entity.Property(binding => binding.ProviderCustomerId).HasMaxLength(255).IsRequired();
            entity.Property(binding => binding.ProviderSubscriptionId).HasMaxLength(255);
            entity.Property(binding => binding.InitialInvoiceId).HasMaxLength(255);
            entity.Property(binding => binding.OfferingCode).HasMaxLength(80).IsRequired();
            entity.Property(binding => binding.ProviderRevision).HasMaxLength(120);
            entity.Property(binding => binding.Revision).IsConcurrencyToken();
            entity.HasIndex(binding => new
            { binding.Provider, binding.Environment, binding.ProviderCustomerId }).IsUnique();
            entity.HasIndex(binding => new
            { binding.Provider, binding.Environment, binding.ProviderSubscriptionId }).IsUnique();
            entity.HasIndex(binding => new
            { binding.Provider, binding.Environment, binding.InitialInvoiceId }).IsUnique();
            entity.HasOne<Organization>().WithOne()
                .HasForeignKey<BillingAccountBinding>(binding => binding.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingPurchaseAttempt>(entity =>
        {
            entity.ToTable("billing_purchase_attempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Provider).HasMaxLength(32).IsRequired();
            entity.Property(attempt => attempt.Environment).HasConversion<string>().HasMaxLength(16);
            entity.Property(attempt => attempt.OfferingCode).HasMaxLength(80).IsRequired();
            entity.Property(attempt => attempt.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(attempt => attempt.ActiveSlot).HasMaxLength(16);
            entity.Property(attempt => attempt.ProviderSessionId).HasMaxLength(255);
            entity.Property(attempt => attempt.Revision).IsConcurrencyToken();
            entity.HasIndex(attempt => new { attempt.OrganizationId, attempt.ActiveSlot }).IsUnique();
            entity.HasIndex(attempt => new
            { attempt.Provider, attempt.Environment, attempt.ProviderSessionId }).IsUnique();
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(attempt => attempt.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingEventReceipt>(entity =>
        {
            entity.ToTable("billing_event_receipts");
            entity.HasKey(receipt => receipt.Id);
            entity.Property(receipt => receipt.Provider).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.Environment).HasConversion<string>().HasMaxLength(16);
            entity.Property(receipt => receipt.ProviderEventId).HasMaxLength(255).IsRequired();
            entity.Property(receipt => receipt.EventType).HasMaxLength(100).IsRequired();
            entity.Property(receipt => receipt.ProviderObjectId).HasMaxLength(255).IsRequired();
            entity.Property(receipt => receipt.ApiVersion).HasMaxLength(40);
            entity.Property(receipt => receipt.PayloadSha256).HasMaxLength(64).IsRequired();
            entity.Property(receipt => receipt.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(receipt => receipt.OutcomeCode).HasMaxLength(80).IsRequired();
            entity.Property(receipt => receipt.Revision).IsConcurrencyToken();
            entity.HasIndex(receipt => new
            { receipt.Provider, receipt.Environment, receipt.ProviderEventId }).IsUnique();
            entity.HasIndex(receipt => new { receipt.State, receipt.ReceivedAt });
        });

        modelBuilder.Entity<BillingAttention>(entity =>
        {
            entity.ToTable("billing_attentions");
            entity.HasKey(attention => attention.Id);
            entity.Property(attention => attention.ReasonCode).HasMaxLength(80).IsRequired();
            entity.Property(attention => attention.Revision).IsConcurrencyToken();
            entity.HasIndex(attention => new { attention.OrganizationId, attention.ResolvedAt });
            entity.HasOne<Organization>().WithMany()
                .HasForeignKey(attention => attention.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void EnsureAuditsAreAppendOnly()
    {
        if (ChangeTracker.Entries<CommercialAuditEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<AccountAuditEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<SupportAuditEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
    }
}
