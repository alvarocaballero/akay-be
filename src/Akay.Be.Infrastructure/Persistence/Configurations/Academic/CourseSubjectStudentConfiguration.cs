using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectStudentConfiguration : IEntityTypeConfiguration<CourseSubjectStudent>
{
    public void Configure(EntityTypeBuilder<CourseSubjectStudent> builder)
    {
        builder.ToTable("CourseSubjectStudent", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseSubjectId)
            .IsRequired();

        builder.Property(x => x.StudentCourseId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.StudentCourse)
            .WithMany()
            .HasForeignKey(x => x.StudentCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourseSubjectId, x.StudentCourseId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubjectStudent_CourseSubjectId_StudentCourseId")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
