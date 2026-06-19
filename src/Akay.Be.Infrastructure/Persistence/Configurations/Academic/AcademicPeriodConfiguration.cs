using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class AcademicPeriodConfiguration : IEntityTypeConfiguration<AcademicPeriod>
{
    public void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        builder.ToTable("AcademicPeriod", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.CenterId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StartDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Center)
            .WithMany()
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CenterId)
            .HasDatabaseName("IX_AcademicPeriod_CenterId");
    }
}
