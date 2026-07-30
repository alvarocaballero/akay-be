using Akay.Be.Application.Features.Students;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Academic;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Requests;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public class AdminAcademicIntegrationTests
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

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAuthenticated => false;
        public int UserId => 0;
        public string Name => string.Empty;
        public string Email => string.Empty;
        public IEnumerable<string> Roles => Array.Empty<string>();
    }

    [Fact]
    public async Task SubjectRepository_GetByCenterIdsAsync_FiltersByCenters()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new SubjectRepository(ctx);

        ctx.Subjects.Add(Subject.Create("Math", null, [1, 2]));
        ctx.Subjects.Add(Subject.Create("History", null, [2]));
        ctx.Subjects.Add(Subject.Create("Science", null, [3]));
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetByCenterIdsAsync([1, 3], ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "Math");
        Assert.Contains(result, s => s.Name == "Science");
    }

    [Fact]
    public async Task SubjectRepository_SubjectIsAvailableForCenterAsync_RespectsSoftDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new SubjectRepository(ctx);

        var subject = Subject.Create("Math", null, [1, 2]);
        ctx.Subjects.Add(subject);
        await ctx.SaveChangesAsync(ct);

        Assert.True(await repo.SubjectIsAvailableForCenterAsync(subject.Id, 1, ct));

        subject.RemoveCenter(1);
        await ctx.SaveChangesAsync(ct);

        Assert.False(await repo.SubjectIsAvailableForCenterAsync(subject.Id, 1, ct));
    }

    [Fact]
    public async Task CourseRepository_GetWithStudentsAsync_WithTracking_PersistsUnenrollment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new CourseRepository(ctx);
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(100);

        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync(ct);
        ctx.ChangeTracker.Clear();

        var trackedCourse = await repo.GetWithStudentsAsync(course.Id, false, ct);
        trackedCourse!.UnenrollStudent(100);
        await ctx.SaveChangesAsync(ct);

        var enrollment = await ctx.StudentCourses.IgnoreQueryFilters().SingleAsync(ct);
        Assert.NotNull(enrollment.DeletedAt);
    }

    [Fact]
    public async Task AcademicPeriodRepository_GetByCenterIdsAsync_FiltersByCenters()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new AcademicPeriodRepository(ctx);

        ctx.AcademicPeriods.Add(AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        ctx.AcademicPeriods.Add(AcademicPeriod.Create(2, "P2", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        ctx.AcademicPeriods.Add(AcademicPeriod.Create(3, "P3", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetByCenterIdsAsync([1, 3], ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CourseRepository_GetByCenterIdsAsync_FiltersThroughAcademicPeriods()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new CourseRepository(ctx);

        var center1 = Center.Create("C1", "C1");
        var center2 = Center.Create("C2", "C2");
        ctx.Centers.AddRange(center1, center2);
        await ctx.SaveChangesAsync(ct);

        var period1 = AcademicPeriod.Create(center1.Id, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var period2 = AcademicPeriod.Create(center2.Id, "P2", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        ctx.AcademicPeriods.AddRange(period1, period2);
        await ctx.SaveChangesAsync(ct);

        ctx.Courses.Add(Course.Create(period1.Id, "Course 1", "C01"));
        ctx.Courses.Add(Course.Create(period2.Id, "Course 2", "C02"));
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetByCenterIdsAsync([center2.Id], ct);

        Assert.Single(result);
        Assert.Equal("Course 2", result[0].Name);
    }

    [Fact]
    public async Task CourseRepository_GetCenterIdAsync_ResolvesCenterThroughAcademicPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new CourseRepository(ctx);

        var center = Center.Create("Center", "CEN");
        ctx.Centers.Add(center);
        await ctx.SaveChangesAsync(ct);

        var period = AcademicPeriod.Create(center.Id, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        ctx.AcademicPeriods.Add(period);
        await ctx.SaveChangesAsync(ct);

        var course = Course.Create(period.Id, "Course", "C01");
        ctx.Courses.Add(course);
        await ctx.SaveChangesAsync(ct);

        var centerId = await repo.GetCenterIdAsync(course.Id, ct);

        Assert.Equal(center.Id, centerId);
    }

    [Fact]
    public async Task StudentRepository_GetByCenterIdsAsync_FiltersByCenters()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new StudentRepository(ctx);

        ctx.Students.Add(Student.Create(1, 1, "S001"));
        ctx.Students.Add(Student.Create(2, 2, "S002"));
        ctx.Students.Add(Student.Create(3, 3, "S003"));
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetByCenterIdsAsync([1, 3], ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task StudentRepository_GetPagedByAdminScopeAsync_FiltersByAdminCenters()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new StudentRepository(ctx);

        var users = await SeedUsersAsync(ctx, 3, ct);
        ctx.Students.Add(Student.Create(users[0], 1, "S001"));
        ctx.Students.Add(Student.Create(users[1], 2, "S002"));
        ctx.Students.Add(Student.Create(users[2], 3, "S003"));
        await ctx.SaveChangesAsync(ct);

        var filter = new StudentListFilter(new HashSet<int> { 1, 3 }, null, null);
        var pageRequest = new PageRequest(1, 10, null, null);

        var result = await repo.GetPagedByAdminScopeAsync(filter, pageRequest, ct);

        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task StudentRepository_GetPagedByAdminScopeAsync_FiltersByCenterId()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new StudentRepository(ctx);

        var users = await SeedUsersAsync(ctx, 3, ct);
        ctx.Students.Add(Student.Create(users[0], 1, "S001"));
        ctx.Students.Add(Student.Create(users[1], 2, "S002"));
        ctx.Students.Add(Student.Create(users[2], 3, "S003"));
        await ctx.SaveChangesAsync(ct);

        var filter = new StudentListFilter(new HashSet<int> { 2 }, null, null);
        var pageRequest = new PageRequest(1, 10, null, null);

        var result = await repo.GetPagedByAdminScopeAsync(filter, pageRequest, ct);

        Assert.Single(result.Data);
        Assert.Equal("S002", result.Data[0].StudentNumber);
    }

    [Fact]
    public async Task StudentRepository_GetPagedByAdminScopeAsync_FiltersBySearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new StudentRepository(ctx);

        var users = await SeedUsersAsync(ctx, 2, ct);
        ctx.Students.Add(Student.Create(users[0], 1, "S001"));
        ctx.Students.Add(Student.Create(users[1], 2, "S002"));
        await ctx.SaveChangesAsync(ct);

        var filter = new StudentListFilter(new HashSet<int> { 1, 2 }, "S002", null);
        var pageRequest = new PageRequest(1, 10, null, null);

        var result = await repo.GetPagedByAdminScopeAsync(filter, pageRequest, ct);

        Assert.Single(result.Data);
        Assert.Equal("S002", result.Data[0].StudentNumber);
    }

    [Fact]
    public async Task StudentRepository_GetPagedByAdminScopeAsync_RespectsPaging()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new StudentRepository(ctx);

        var users = await SeedUsersAsync(ctx, 3, ct);
        ctx.Students.Add(Student.Create(users[0], 1, "S001"));
        ctx.Students.Add(Student.Create(users[1], 1, "S002"));
        ctx.Students.Add(Student.Create(users[2], 1, "S003"));
        await ctx.SaveChangesAsync(ct);

        var filter = new StudentListFilter(new HashSet<int> { 1 }, null, null);
        var pageRequest = new PageRequest(1, 2, null, null);

        var result = await repo.GetPagedByAdminScopeAsync(filter, pageRequest, ct);

        Assert.Equal(2, result.Data.Count);
        Assert.True(result.HasMoreItems);
    }

    private static async Task<List<int>> SeedUsersAsync(ApplicationDbContext ctx, int count, CancellationToken ct)
    {
        var users = new List<User>();
        for (int i = 1; i <= count; i++)
        {
            users.Add(User.Create($"user{i}@test.com", $"First{i}", $"Last{i}"));
        }
        ctx.Set<User>().AddRange(users);
        await ctx.SaveChangesAsync(ct);
        return users.Select(u => u.Id).ToList();
    }
}
