using Akay.Be.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akay.Be.Infrastructure.Persistence.Configurations.Academic;

internal sealed class CourseSubjectConfiguration : IEntityTypeConfiguration<CourseSubject>
{
    public void Configure(EntityTypeBuilder<CourseSubject> builder)
    {
        builder.ToTable("CourseSubject", "academic");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => new { x.CourseId, x.SubjectId })
            .IsUnique()
            .HasDatabaseName("IX_CourseSubject_CourseId_SubjectId")
            .HasFilter("[DeletedAt] IS NULL");

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
