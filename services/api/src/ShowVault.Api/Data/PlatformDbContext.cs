using Microsoft.EntityFrameworkCore;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;

namespace ShowVault.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Venue> Venues => Set<Venue>();

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
    }
}
