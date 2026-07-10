using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Organization;
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
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSubject> CourseSubjects => Set<CourseSubject>();
    public DbSet<CourseSubjectTeacher> CourseSubjectTeachers => Set<CourseSubjectTeacher>();
    public DbSet<CourseSubjectStudent> CourseSubjectStudents => Set<CourseSubjectStudent>();
    public DbSet<StudentCourse> StudentCourses => Set<StudentCourse>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectCenter> SubjectCenters => Set<SubjectCenter>();
    public DbSet<SubjectAdmin> SubjectAdmins => Set<SubjectAdmin>();
    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyDbContextSettings(this);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
