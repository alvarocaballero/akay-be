using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topic", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(x => x.ProcessingState)
            .HasColumnName("processing_state")
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.IndexingState)
            .HasColumnName("indexing_state")
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => new { x.SubjectId, x.DisplayOrder })
            .HasDatabaseName("IX_Topic_SubjectId_DisplayOrder");

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Contents)
            .WithOne(x => x.Topic)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
