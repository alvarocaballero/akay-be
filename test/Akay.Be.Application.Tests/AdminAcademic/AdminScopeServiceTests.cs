using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Contexts;
using Moq;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class AdminScopeServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AdminScopeService CreateService(
        int currentUserId,
        Dictionary<int, List<UserRole>>? rolesByCenter = null,
        Subject? subject = null,
        AcademicPeriod? period = null,
        Course? course = null,
        Student? student = null)
    {
        var userContext = new Mock<IUserContext>();
        userContext.Setup(x => x.UserId).Returns(currentUserId);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetUserRolesByCentersAsync(currentUserId, Ct))
            .ReturnsAsync(rolesByCenter ?? []);

        var subjectRepo = new Mock<ISubjectRepository>();
        if (subject is not null)
            subjectRepo.Setup(x => x.GetWithCentersAsync(subject.Id, Ct)).ReturnsAsync(subject);

        var periodRepo = new Mock<IAcademicPeriodRepository>();
        if (period is not null)
            periodRepo.Setup(x => x.GetByIdAsync(period.Id, Ct)).ReturnsAsync(period);

        var courseRepo = new Mock<ICourseRepository>();
        if (course is not null)
        {
            courseRepo.Setup(x => x.GetCenterIdAsync(course.Id, Ct)).ReturnsAsync(course.AcademicPeriod?.CenterId);
        }

        var studentRepo = new Mock<IStudentRepository>();
        if (student is not null)
            studentRepo.Setup(x => x.GetByUserIdAndCenterIdAsync(student.UserId, student.CenterId, Ct)).ReturnsAsync(student);

        return new AdminScopeService(userContext.Object, userRepo.Object, subjectRepo.Object, periodRepo.Object, courseRepo.Object, studentRepo.Object);
    }

    [Fact]
    public async Task GetAdminCenterIdsAsync_ReturnsOnlyAdminCenters()
    {
        var service = CreateService(1, new Dictionary<int, List<UserRole>>
        {
            [1] = [UserRole.Admin],
            [2] = [UserRole.Teacher],
            [3] = [UserRole.Admin, UserRole.Teacher]
        });

        var centers = await service.GetAdminCenterIdsAsync(Ct);

        Assert.Equal(2, centers.Count);
        Assert.Contains(1, centers);
        Assert.Contains(3, centers);
    }

    [Fact]
    public async Task EnsureAdminOfCenterAsync_SucceedsForAdminCenter()
    {
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [1] = [UserRole.Admin] });

        var result = await service.EnsureAdminOfCenterAsync(1, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAdminOfCenterAsync_ForbidsNonAdminCenter()
    {
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [1] = [UserRole.Admin] });

        var result = await service.EnsureAdminOfCenterAsync(8, Ct);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EnsureCanAccessSubjectAsync_SucceedsWhenCenterOverlap()
    {
        var subject = Subject.Create("Math", null, [1, 8]);
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [1] = [UserRole.Admin] }, subject: subject);

        var result = await service.EnsureCanAccessSubjectAsync(subject.Id, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureCanAccessSubjectAsync_ForbidsWhenNoOverlap()
    {
        var subject = Subject.Create("Math", null, [8]);
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [1] = [UserRole.Admin] }, subject: subject);

        var result = await service.EnsureCanAccessSubjectAsync(subject.Id, Ct);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EnsureCanAccessStudentAsync_UsesUserIdAndCenterId()
    {
        var student = Student.Create(10, 2);
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [2] = [UserRole.Teacher] }, student: student);

        var result = await service.EnsureCanAccessStudentAsync(10, 2, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureCanAccessCourseAsync_SucceedsForAdminCenter()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        var service = CreateService(1, new Dictionary<int, List<UserRole>> { [1] = [UserRole.Admin] }, course: course);

        var result = await service.EnsureCanAccessCourseAsync(course.Id, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UserHasRoleInCenterAsync_DelegatesToRepository()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetUserRolesByCentersAsync(1, Ct)).ReturnsAsync([]);
        userRepo.Setup(x => x.UserHasActiveRoleInCenterAsync(10, 1, UserRole.Teacher, Ct)).ReturnsAsync(true);

        var userContext = new Mock<IUserContext>();
        userContext.Setup(x => x.UserId).Returns(1);

        var service = new AdminScopeService(
            userContext.Object,
            userRepo.Object,
            Mock.Of<ISubjectRepository>(),
            Mock.Of<IAcademicPeriodRepository>(),
            Mock.Of<ICourseRepository>(),
            Mock.Of<IStudentRepository>());

        var result = await service.UserHasRoleInCenterAsync(10, 1, UserRole.Teacher, Ct);

        Assert.True(result);
    }
}
