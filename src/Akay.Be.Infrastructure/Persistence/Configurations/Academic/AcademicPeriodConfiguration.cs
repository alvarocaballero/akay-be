using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class AcademicPeriodConfiguration : IEntityTypeConfiguration<AcademicPeriod>
{
    public void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        builder.ToTable("AcademicPeriod", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CenterId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => new { x.CenterId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_AcademicPeriod_CenterId_Name")
            .HasFilter("[DeletedAt] IS NULL");

        builder.HasOne(x => x.Center)
            .WithMany(x => x.AcademicPeriods)
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
