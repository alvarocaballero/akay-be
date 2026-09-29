using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class SubjectCenterConfiguration : IEntityTypeConfiguration<SubjectCenter>
{
    public void Configure(EntityTypeBuilder<SubjectCenter> builder)
    {
        builder.ToTable("subject_center", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.CenterId)
            .HasColumnName("center_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne(x => x.Center)
            .WithMany()
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SubjectId, x.CenterId })
            .IsUnique()
            .HasDatabaseName("IX_SubjectCenter_SubjectId_CenterId")
            .HasFilter("deleted_at IS NULL");
    }
}
