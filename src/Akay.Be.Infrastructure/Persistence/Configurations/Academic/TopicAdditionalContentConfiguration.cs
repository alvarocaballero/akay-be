using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class TopicAdditionalContentConfiguration : IEntityTypeConfiguration<TopicAdditionalContent>
{
    public void Configure(EntityTypeBuilder<TopicAdditionalContent> builder)
    {
        builder.ToTable("topic_additional_content", "academic");

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

        builder.Property(x => x.SourceContentUrl)
            .HasColumnName("source_content_url");

        builder.Property(x => x.ContentUrl)
            .HasColumnName("content_url");

        builder.Property(x => x.IsVisible)
            .HasColumnName("is_visible");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => x.TopicId)
            .HasDatabaseName("IX_TopicAdditionalContent_TopicId");

        builder.HasOne(x => x.Topic)
            .WithMany(x => x.AdditionalContents)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
