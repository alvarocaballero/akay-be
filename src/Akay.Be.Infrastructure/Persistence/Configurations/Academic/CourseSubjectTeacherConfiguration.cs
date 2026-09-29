using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectTeacherConfiguration : IEntityTypeConfiguration<CourseSubjectTeacher>
{
    public void Configure(EntityTypeBuilder<CourseSubjectTeacher> builder)
    {
        builder.ToTable("course_subject_teacher", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CourseSubjectId)
            .HasColumnName("course_subject_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourseSubjectId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubjectTeacher_CourseSubjectId_UserId")
            .HasFilter("deleted_at IS NULL");
    }
}
