using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class StudentCourseSubjectConfiguration : IEntityTypeConfiguration<StudentCourseSubject>
{
    public void Configure(EntityTypeBuilder<StudentCourseSubject> builder)
    {
        builder.ToTable("StudentCourseSubject", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.StudentCourseId)
            .IsRequired();

        builder.Property(x => x.CourseSubjectId)
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

        builder.HasOne(x => x.StudentCourse)
            .WithMany(x => x.StudentCourseSubjects)
            .HasForeignKey(x => x.StudentCourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CourseSubject)
            .WithMany(x => x.StudentCourseSubjects)
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentCourseId, x.CourseSubjectId })
            .IsUnique()
            .HasDatabaseName("UX_StudentCourseSubject_StudentCourseId_CourseSubjectId");
    }
}
