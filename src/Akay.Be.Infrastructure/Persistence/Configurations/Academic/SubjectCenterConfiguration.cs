using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class SubjectCenterConfiguration : IEntityTypeConfiguration<SubjectCenter>
{
    public void Configure(EntityTypeBuilder<SubjectCenter> builder)
    {
        builder.ToTable("SubjectCenter", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.CenterId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Center)
            .WithMany()
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SubjectId, x.CenterId })
            .IsUnique()
            .HasDatabaseName("IX_SubjectCenter_SubjectId_CenterId")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
