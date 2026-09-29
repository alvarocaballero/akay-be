using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class SubjectAdminConfiguration : IEntityTypeConfiguration<SubjectAdmin>
{
    public void Configure(EntityTypeBuilder<SubjectAdmin> builder)
    {
        builder.ToTable("subject_admin", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SubjectId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_SubjectAdmin_SubjectId_UserId")
            .HasFilter("deleted_at IS NULL");
    }
}
