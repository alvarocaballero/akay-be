using Akay.Be.Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Notification;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<PersistentNotification>
{
    public void Configure(EntityTypeBuilder<PersistentNotification> builder)
    {
        builder.ToTable("notification", "notification");

        builder.HasKey(n => n.Id)
               .HasName("PK_notifications");

        builder.Property(n => n.Id)
               .HasColumnName("id")
               .ValueGeneratedOnAdd();

        builder.Property(n => n.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(n => n.Type)
               .HasColumnName("type")
               .IsRequired();

        builder.Property(n => n.Title)
               .HasColumnName("title")
               .IsRequired();

        builder.Property(n => n.Message)
               .HasColumnName("message")
               .IsRequired();

        builder.Property(n => n.Url)
               .HasColumnName("url");

        builder.Property(n => n.Payload)
               .HasColumnName("payload")
               .HasColumnType("jsonb");

        builder.Property(n => n.CreatedAt)
               .HasColumnName("created_at")
               .HasColumnType("timestamp with time zone");

        builder.Property(n => n.ReadAt)
               .HasColumnName("read_at")
               .HasColumnType("timestamp with time zone");

        builder.HasIndex(n => new { n.UserId, n.CreatedAt, n.Id })
               .HasDatabaseName("IX_Notification_UserId_CreatedAt")
               .IsDescending(false, true, true);

        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
               .HasDatabaseName("IX_Notification_UserId_Unread")
               .IsDescending(false, true)
               .HasFilter("read_at IS NULL");
    }
}
