using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectConfiguration : IEntityTypeConfiguration<CourseSubject>
{
    public void Configure(EntityTypeBuilder<CourseSubject> builder)
    {
        builder.ToTable("course_subject", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CourseId)
            .HasColumnName("course_id")
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => new { x.CourseId, x.SubjectId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubject_CourseId_SubjectId")
            .HasFilter("deleted_at IS NULL");

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Teachers)
            .WithOne(x => x.CourseSubject)
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Students)
            .WithOne(x => x.CourseSubject)
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
