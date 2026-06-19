using Akay.Be.Domain.Aggregates.Academic;
using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Aggregates.Organization;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.EF.Infrastructure.DbContexts;
using Akay.To.EF.Infrastructure.ModelBuilding;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext(IUserContext userContext,
                                         DbContextRegistration<ApplicationDbContext> registration,
                                         DbContextOptions<ApplicationDbContext> options)
    : BaseDbContext<ApplicationDbContext>(userContext, registration, options)
{
    public DbSet<Domain.Aggregates.Organization.Organization> Organizations =>
        Set<Domain.Aggregates.Organization.Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<CourseSubject> CourseSubjects => Set<CourseSubject>();

    public DbSet<AdminCourseSubject> AdminCourseSubjects => Set<AdminCourseSubject>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<StudentCourse> StudentCourses => Set<StudentCourse>();

    public DbSet<StudentCourseSubject> StudentCourseSubjects => Set<StudentCourseSubject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyDbContextSettings(this);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
