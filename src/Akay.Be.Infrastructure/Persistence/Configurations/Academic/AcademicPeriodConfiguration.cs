using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class AcademicPeriodConfiguration : IEntityTypeConfiguration<AcademicPeriod>
{
    public void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        builder.ToTable("academic_period", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CenterId)
            .HasColumnName("center_id")
            .IsRequired();

        builder.Property(x => x.SyncId)
            .HasColumnName("sync_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => new { x.CenterId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_AcademicPeriod_CenterId_Name")
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(x => x.SyncId)
            .IsUnique()
            .HasDatabaseName("IX_AcademicPeriod_SyncId");

        builder.HasOne(x => x.Center)
            .WithMany(x => x.AcademicPeriods)
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
