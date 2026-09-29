using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectStudentConfiguration : IEntityTypeConfiguration<CourseSubjectStudent>
{
    public void Configure(EntityTypeBuilder<CourseSubjectStudent> builder)
    {
        builder.ToTable("course_subject_student", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CourseSubjectId)
            .HasColumnName("course_subject_id")
            .IsRequired();

        builder.Property(x => x.StudentCourseId)
            .HasColumnName("student_course_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne(x => x.StudentCourse)
            .WithMany()
            .HasForeignKey(x => x.StudentCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourseSubjectId, x.StudentCourseId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubjectStudent_CourseSubjectId_StudentCourseId")
            .HasFilter("deleted_at IS NULL");
    }
}
