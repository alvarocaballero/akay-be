using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class StudentCourseConfiguration : IEntityTypeConfiguration<StudentCourse>
{
    public void Configure(EntityTypeBuilder<StudentCourse> builder)
    {
        builder.ToTable("StudentCourse", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.EnrolledAt)
            .IsRequired()
            .HasColumnType("datetime2");

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.StudentCourses)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Course)
            .WithMany(x => x.StudentCourses)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentId, x.CourseId })
            .IsUnique()
            .HasDatabaseName("UX_StudentCourse_StudentId_CourseId");

        builder.HasMany(x => x.StudentCourseSubjects)
            .WithOne(x => x.StudentCourse)
            .HasForeignKey(x => x.StudentCourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
