using Akay.Be.Domain.Aggregates.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

public class AdminCourseSubjectConfiguration : IEntityTypeConfiguration<AdminCourseSubject>
{
    public void Configure(EntityTypeBuilder<AdminCourseSubject> builder)
    {
        builder.ToTable("AdminCourseSubject", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .UseIdentityColumn();

        builder.Property(x => x.CourseSubjectId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.CourseSubject)
            .WithMany()
            .HasForeignKey(x => x.CourseSubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_AdminCourseSubject_UserId");

        builder.HasIndex(x => new { x.CourseSubjectId, x.UserId })
            .IsUnique()
            .HasDatabaseName("UX_AdminCourseSubject_CourseSubjectId_UserId");
    }
}
