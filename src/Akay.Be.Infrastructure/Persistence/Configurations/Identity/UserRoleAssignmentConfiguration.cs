using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_role_assignment", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.CenterId)
            .HasColumnName("center_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => new { x.UserId, x.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserRoleAssignment_UserId_Role_Global")
            .HasFilter("center_id IS NULL AND deleted_at IS NULL");

        builder.HasIndex(x => new { x.UserId, x.CenterId, x.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserRoleAssignment_UserId_CenterId_Role")
            .HasFilter("center_id IS NOT NULL AND deleted_at IS NULL");

        builder.ToTable(t => t.HasCheckConstraint("CK_UserRoleAssignment_Role_CenterId",
            "(role = 1 AND center_id IS NULL) OR (role IN (2, 3, 4) AND center_id IS NOT NULL)"));
    }
}
