using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignment", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CenterId);

        builder.Property(x => x.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => new { x.UserId, x.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserRoleAssignment_UserId_Role_Global")
            .HasFilter("[CenterId] IS NULL AND [DeletedAt] IS NULL");

        builder.HasIndex(x => new { x.UserId, x.CenterId, x.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserRoleAssignment_UserId_CenterId_Role")
            .HasFilter("[CenterId] IS NOT NULL AND [DeletedAt] IS NULL");

        builder.ToTable(t => t.HasCheckConstraint("CK_UserRoleAssignment_Role_CenterId",
            "([Role] = 1 AND [CenterId] IS NULL) OR ([Role] IN (2, 3, 4) AND [CenterId] IS NOT NULL)"));
    }
}
