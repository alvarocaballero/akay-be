using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Course", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.CenterId)
            .IsRequired();

        builder.Property(x => x.AcademicPeriodId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Center)
            .WithMany(x => x.Courses)
            .HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicPeriod)
            .WithMany()
            .HasForeignKey(x => x.AcademicPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CenterId, x.AcademicPeriodId, x.Name })
            .IsUnique()
            .HasDatabaseName("UX_Course_CenterId_AcademicPeriodId_Name");

        builder.HasMany(x => x.CourseSubjects)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StudentCourses)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
