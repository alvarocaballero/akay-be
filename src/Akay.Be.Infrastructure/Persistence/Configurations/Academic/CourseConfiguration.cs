using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable(nameof(Course), "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AcademicPeriodId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => new { x.AcademicPeriodId, x.Code })
            .IsUnique()
            .HasDatabaseName("IX_Course_AcademicPeriodId_Code")
            .HasFilter("[DeletedAt] IS NULL");

        builder.HasOne(x => x.AcademicPeriod)
            .WithMany(x => x.Courses)
            .HasForeignKey(x => x.AcademicPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Subjects)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Students)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
