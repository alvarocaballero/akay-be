using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class TopicContentConfiguration : IEntityTypeConfiguration<TopicContent>
{
    public void Configure(EntityTypeBuilder<TopicContent> builder)
    {
        builder.ToTable("topic_content", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.TopicId)
            .HasColumnName("topic_id")
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.OriginalContentUrl)
            .HasColumnName("original_content_url");

        builder.Property(x => x.ContentUrl)
            .HasColumnName("content_url");

        builder.Property(x => x.ProcessingState)
            .HasColumnName("processing_state")
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.IndexingState)
            .HasColumnName("indexing_state")
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.ProcessingError)
            .HasColumnName("processing_error");

        builder.Property(x => x.IndexingError)
            .HasColumnName("indexing_error");

        builder.Property(x => x.ProcessingAttemptId)
            .HasColumnName("processing_attempt_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => x.TopicId)
            .HasDatabaseName("IX_TopicContent_TopicId");

        builder.HasIndex(x => new { x.TopicId, x.ProcessingState })
            .HasDatabaseName("IX_TopicContent_ActiveProcessing")
            .HasFilter("deleted_at IS NULL");

        builder.HasOne(x => x.Topic)
            .WithMany(x => x.Contents)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
