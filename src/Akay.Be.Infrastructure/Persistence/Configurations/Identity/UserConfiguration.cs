using Akay.Be.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(nameof(User), "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalId);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("IX_User_Email")
            .HasFilter("[DeletedAt] IS NULL");

        builder.HasIndex(x => x.ExternalId)
            .IsUnique()
            .HasDatabaseName("IX_User_ExternalId")
            .HasFilter("[ExternalId] IS NOT NULL AND [DeletedAt] IS NULL");

        builder.HasMany(x => x.RoleAssignments)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
