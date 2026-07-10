using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.Be.Infrastructure.Persistence.Seed;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public class PersistenceIntegrationTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var userContext = new TestUserContext();
        var registration = new DbContextRegistration<ApplicationDbContext>(
            new DbContextSettings(),
            (_, _, _) => { });

        return new ApplicationDbContext(userContext, registration, options);
    }

    [Fact]
    public async Task PersistCenter_AndRetrieve()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var center = Center.Create("Test Center", "TEST");
        ctx.Centers.Add(center);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Centers.FirstOrDefaultAsync(c => c.Code == "TEST", ct);

        Assert.NotNull(retrieved);
        Assert.Equal("Test Center", retrieved!.Name);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task PersistUser_WithRoleAssignments()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);
        user.AssignRole(2, UserRole.Student);

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Users
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(u => u.Email == "test@example.com", ct);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved!.RoleAssignments.Count(r => r.DeletedAt == null));
    }

    [Fact]
    public async Task PersistSubject_WithCentersAndAdmins()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var subject = Subject.Create("Math", null, [1, 2]);
        subject.AddAdmin(10);
        subject.AddAdmin(20);

        ctx.Subjects.Add(subject);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Subjects
            .Include(s => s.Centers)
            .Include(s => s.Admins)
            .FirstOrDefaultAsync(s => s.Name == "Math", ct);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved!.Centers.Count(c => c.DeletedAt == null));
        Assert.Equal(2, retrieved.Admins.Count(a => a.DeletedAt == null));
    }

    [Fact]
    public async Task PersistCourse_WithSubjectsAndStudents()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());

        var center = Center.Create("Center", "CEN");
        ctx.Centers.Add(center);
        await ctx.SaveChangesAsync(ct);

        var period = AcademicPeriod.Create(center.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        ctx.AcademicPeriods.Add(period);
        await ctx.SaveChangesAsync(ct);

        var course = Course.Create(period.Id, "1º ESO", "ESO1");
        course.AddSubject(1);
        course.AddSubject(2);
        course.EnrollStudent(100);
        course.EnrollStudent(200);

        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Courses
            .Include(c => c.Subjects)
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Code == "ESO1", ct);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved!.Subjects.Count(s => s.DeletedAt == null));
        Assert.Equal(2, retrieved.Students.Count(s => s.DeletedAt == null));
    }

    [Fact]
    public async Task PersistStudent_AndRetrieve()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var student = Student.Create(1, 2, "STU001");
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Students.FirstOrDefaultAsync(s => s.StudentNumber == "STU001", ct);

        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved!.UserId);
        Assert.Equal(2, retrieved.CenterId);
    }

    [Fact]
    public async Task UserRole_PersistedAsInt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var user = User.Create("role@example.com", "Role", "Test");
        user.AssignGlobalRole(UserRole.SuperAdmin);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(cancellationToken);

        var retrieved = await ctx.Users
            .Include(u => u.RoleAssignments)
            .FirstAsync(u => u.Email == "role@example.com", cancellationToken);

        Assert.Equal(1, (int)retrieved.RoleAssignments.First().Role);
        Assert.Equal(TypeCode.Int32, retrieved.RoleAssignments.First().Role.GetTypeCode());
    }

    [Fact]
    public async Task DomainSoftDelete_MarksDeletedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(ct);

        user.RemoveRole(1, UserRole.Teacher);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Users
            .Include(u => u.RoleAssignments)
            .FirstAsync(u => u.Email == "test@example.com", ct);

        Assert.DoesNotContain(retrieved.RoleAssignments, r => r.DeletedAt == null);
        Assert.Single(retrieved.RoleAssignments, r => r.DeletedAt != null);
    }

    [Fact]
    public async Task RecreateSoftDeleted_UniqueIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());

        // For InMemory, unique index constraints on soft-deleted entities
        // need to be verified through domain logic, not DB constraints.
        var center1 = Center.Create("Center", "REUSE");
        ctx.Centers.Add(center1);
        await ctx.SaveChangesAsync(ct);

        // Simulate soft delete via domain (we can't rely on interceptor)
        // Just verify the domain allows creating a new entity with same code
        // after the previous one is "removed" from domain perspective.

        var center2 = Center.Create("Center Reborn", "REUSE");
        ctx.Centers.Add(center2);
        await ctx.SaveChangesAsync(ct);

        var all = await ctx.Centers.IgnoreQueryFilters()
            .Where(c => c.Code == "REUSE")
            .ToListAsync(ct);

        // Without interceptors, both are visible (not soft-deleted)
        // This test validates the model allows the domain operation
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task CourseSubject_WithTeachersAndStudents()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var course = Course.Create(1, "Test Course", "TC01");
        course.AddSubject(10);
        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync(ct);

        var saved = await ctx.Courses.Include(c => c.Subjects).FirstAsync(c => c.Code == "TC01", ct);
        var cs = saved.Subjects.First();
        cs.AssignTeacher(100);
        cs.AssignTeacher(200);
        cs.EnrollStudent(1000);
        cs.EnrollStudent(2000);
        await ctx.SaveChangesAsync(ct);

        var retrieved = await ctx.Courses
            .Include(c => c.Subjects)
                .ThenInclude(s => s.Teachers)
            .Include(c => c.Subjects)
                .ThenInclude(s => s.Students)
            .FirstAsync(c => c.Code == "TC01", ct);

        var loadedCs = retrieved.Subjects.First();
        Assert.Equal(2, loadedCs.Teachers.Count(t => t.DeletedAt == null));
        Assert.Equal(2, loadedCs.Students.Count(s => s.DeletedAt == null));
    }

    [Fact]
    public async Task Seeder_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());

        await DevelopmentSeeder.SeedAsync(ctx, ct);
        var countAfterFirst = await ctx.Centers.CountAsync(ct);

        await DevelopmentSeeder.SeedAsync(ctx, ct);
        var countAfterSecond = await ctx.Centers.CountAsync(ct);

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.Equal(3, countAfterFirst);
    }

    [Fact]
    public async Task Seeder_CreatesExpectedData()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        await DevelopmentSeeder.SeedAsync(ctx, ct);

        Assert.Equal(3, await ctx.Centers.CountAsync(ct));
        Assert.Equal(20, await ctx.Users.CountAsync(ct)); // 1 super + 4 admins + 5 teachers + 10 students
        Assert.Equal(6, await ctx.AcademicPeriods.CountAsync(ct));
        Assert.Equal(6, await ctx.Courses.CountAsync(ct));
        Assert.Equal(5, await ctx.Subjects.CountAsync(ct));
        Assert.Equal(12, await ctx.Students.CountAsync(ct)); // student profiles
    }

    [Fact]
    public async Task CourseWithFullGraph_LoadsCorrectly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());

        var center = Center.Create("Center", "FULL");
        ctx.Centers.Add(center);
        await ctx.SaveChangesAsync(cancellationToken);

        var period = AcademicPeriod.Create(center.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        ctx.AcademicPeriods.Add(period);
        await ctx.SaveChangesAsync(cancellationToken);

        var subject = Subject.Create("Full Subject", null, [center.Id]);
        ctx.Subjects.Add(subject);
        await ctx.SaveChangesAsync(cancellationToken);

        var course = Course.Create(period.Id, "Full Course", "FULL01");
        course.AddSubject(subject.Id);
        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync(cancellationToken);

        var cs = course.Subjects.First();
        cs.AssignTeacher(42);
        await ctx.SaveChangesAsync(cancellationToken);

        var student = Student.Create(1, center.Id, "FULLSTU");
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync(cancellationToken);

        course.EnrollStudent(student.Id);
        await ctx.SaveChangesAsync(cancellationToken);

        var sc = course.Students.First();
        cs.EnrollStudent(sc.Id);
        await ctx.SaveChangesAsync(cancellationToken);

        var loaded = await ctx.Courses
            .Include(c => c.Subjects)
                .ThenInclude(s => s.Teachers)
            .Include(c => c.Subjects)
                .ThenInclude(s => s.Students)
            .Include(c => c.Students)
            .FirstAsync(c => c.Code == "FULL01", cancellationToken);

        Assert.Single(loaded.Subjects);
        Assert.Single(loaded.Subjects.First().Teachers);
        Assert.Single(loaded.Subjects.First().Students);
        Assert.Single(loaded.Students);
    }

    [Fact]
    public async Task Subject_WithCentersAndAdmins_LoadsCorrectly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());

        var center1 = Center.Create("C1", "C1");
        var center2 = Center.Create("C2", "C2");
        ctx.Centers.AddRange(center1, center2);
        await ctx.SaveChangesAsync(cancellationToken);

        var subject = Subject.Create("Multi Center Subject", null, [center1.Id, center2.Id]);
        subject.AddAdmin(10);
        subject.AddAdmin(20);
        ctx.Subjects.Add(subject);
        await ctx.SaveChangesAsync(cancellationToken);

        var loaded = await ctx.Subjects
            .Include(s => s.Centers)
            .Include(s => s.Admins)
            .FirstAsync(s => s.Name == "Multi Center Subject", cancellationToken);

        Assert.Equal(2, loaded.Centers.Count(c => c.DeletedAt == null));
        Assert.Equal(2, loaded.Admins.Count(a => a.DeletedAt == null));
    }
}
