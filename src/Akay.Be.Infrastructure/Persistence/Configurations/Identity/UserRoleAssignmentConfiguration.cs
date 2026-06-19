using Akay.Be.Domain.Aggregates.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Identity;

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignment", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.RoleId)
            .IsRequired();

        builder.Property(x => x.OrganizationId);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany(x => x.UserRoleAssignments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Organization)
            .WithMany(x => x.UserRoleAssignments)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.OrganizationId, x.RoleId })
            .IsUnique()
            .HasFilter("[OrganizationId] IS NOT NULL")
            .HasDatabaseName("UX_UserRoleAssignment_Scoped");

        builder.HasIndex(x => new { x.UserId, x.RoleId })
            .IsUnique()
            .HasFilter("[OrganizationId] IS NULL")
            .HasDatabaseName("UX_UserRoleAssignment_Global");
    }
}
