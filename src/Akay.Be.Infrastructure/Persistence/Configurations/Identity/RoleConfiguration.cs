using Akay.Be.Domain.Aggregates.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Identity;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Role_Code");

        builder.HasData(
            new { Id = 1, Code = RoleCodes.SuperAdmin, Name = "Super Administrator", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new { Id = 2, Code = RoleCodes.OrganizationAdmin, Name = "Organization Administrator", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new { Id = 3, Code = RoleCodes.CenterAdmin, Name = "Center Administrator", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new { Id = 4, Code = RoleCodes.Teacher, Name = "Teacher", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new { Id = 5, Code = RoleCodes.Student, Name = "Student", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });
    }
}
