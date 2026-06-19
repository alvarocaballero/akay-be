using Akay.Be.Domain.Aggregates.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Organization;

public class OrganizationConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Organization.Organization>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Organization.Organization> builder)
    {
        builder.ToTable("Organization", "organization");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.TenantId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsCenter)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_Organization_TenantId");

        builder.HasIndex(x => new { x.TenantId, x.IsCenter })
            .HasDatabaseName("IX_Organization_TenantId_IsCenter");

        builder.HasMany(x => x.UserRoleAssignments)
            .WithOne(x => x.Organization)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Courses)
            .WithOne(x => x.Center)
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
