using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectTeacherConfiguration : IEntityTypeConfiguration<CourseSubjectTeacher>
{
    public void Configure(EntityTypeBuilder<CourseSubjectTeacher> builder)
    {
        builder.ToTable("CourseSubjectTeacher", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseSubjectId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourseSubjectId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubjectTeacher_CourseSubjectId_UserId")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
