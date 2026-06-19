using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class CourseSubjectConfiguration : IEntityTypeConfiguration<CourseSubject>
{
    public void Configure(EntityTypeBuilder<CourseSubject> builder)
    {
        builder.ToTable("CourseSubject", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Course)
            .WithMany(x => x.CourseSubjects)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Subject)
            .WithMany(x => x.CourseSubjects)
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourseId, x.SubjectId })
            .IsUnique()
            .HasDatabaseName("UX_CourseSubject_CourseId_SubjectId");

        builder.HasMany(x => x.AdminCourseSubjects)
            .WithOne(x => x.CourseSubject)
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StudentCourseSubjects)
            .WithOne(x => x.CourseSubject)
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
