using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class SubjectAdminConfiguration : IEntityTypeConfiguration<SubjectAdmin>
{
    public void Configure(EntityTypeBuilder<SubjectAdmin> builder)
    {
        builder.ToTable("SubjectAdmin", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SubjectId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_SubjectAdmin_SubjectId_UserId")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
