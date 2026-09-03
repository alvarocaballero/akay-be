using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Features.CourseStudents;
using Akay.Be.Application.Features.CourseSubjectStudents;
using Akay.Be.Application.Features.CourseSubjectTeachers;
using Akay.Be.Application.Features.Courses;
using Akay.Be.Application.Features.Subjects;
using Akay.Be.Application.Features.UserRoles;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;
using Akay.Be.Application.Abstractions.Services;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class AdminAcademicHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateSubjectCommandHandler_ReturnsForbidden_WhenCenterNotInAdminScope()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.IsAny<IEnumerable<int>>(), Ct))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var subjectRepo = new Mock<ISubjectRepository>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new CreateSubjectCommandHandler(adminScope.Object, uow.Object, subjectRepo.Object);
        var result = await handler.Handle(new CreateSubjectCommand("Math", null, [8]), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AddCourseSubjectCommandHandler_ReturnsForbidden_WhenSubjectNotAvailableInCourseCenter()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());

        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithFullGraphAsync(1, false, Ct)).ReturnsAsync(course);

        var subjectRepo = new Mock<ISubjectRepository>();
        subjectRepo.Setup(x => x.SubjectIsAvailableForCenterAsync(10, 1, Ct)).ReturnsAsync(false);

        var uow = new Mock<IUnitOfWork>();

        var handler = new AddCourseSubjectCommandHandler(adminScope.Object, uow.Object, courseRepo.Object, subjectRepo.Object);
        var result = await handler.Handle(new AddCourseSubjectCommand(1, 10), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("course.subject_not_available", result.Error.Code);
    }

    [Fact]
    public async Task EnrollCourseStudentCommandHandler_ReturnsForbidden_WhenStudentFromDifferentCenter()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());

        var period = AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, period);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithFullGraphAsync(1, false, Ct)).ReturnsAsync(course);

        var studentRepo = new Mock<IStudentRepository>();
        studentRepo.Setup(x => x.GetByUserIdAsync(100, Ct)).ReturnsAsync([Student.Create(100, 2)]);

        var uow = new Mock<IUnitOfWork>();

        var handler = new EnrollCourseStudentCommandHandler(adminScope.Object, uow.Object, courseRepo.Object, studentRepo.Object);
        var result = await handler.Handle(new EnrollCourseStudentCommand(1, 100), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("course.student_wrong_center", result.Error.Code);
        adminScope.Verify(x => x.EnsureAdminOfCenterAsync(It.IsAny<int>(), Ct), Times.Never);
    }

    [Fact]
    public async Task EnrollCourseStudentCommandHandler_UsesUserId_WhenStudentBelongsToCourseCenter()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());

        var period = AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, period);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithFullGraphAsync(1, false, Ct)).ReturnsAsync(course);

        var studentRepo = new Mock<IStudentRepository>();
        studentRepo.Setup(x => x.GetByUserIdAsync(100, Ct)).ReturnsAsync([Student.Create(100, 1)]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(Ct)).ReturnsAsync(1);

        var handler = new EnrollCourseStudentCommandHandler(adminScope.Object, uow.Object, courseRepo.Object, studentRepo.Object);
        var result = await handler.Handle(new EnrollCourseStudentCommand(1, 100), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, course.Students.Single().UserId);
    }

    [Fact]
    public async Task UnenrollCourseStudentCommandHandler_LoadsTrackedCourseBeforeSaving()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());

        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(100);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithStudentsAsync(1, false, Ct)).ReturnsAsync(course);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(Ct)).ReturnsAsync(1);

        var handler = new UnenrollCourseStudentCommandHandler(adminScope.Object, uow.Object, courseRepo.Object);
        var result = await handler.Handle(new UnenrollCourseStudentCommand(1, 100), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(course.Students.Single().DeletedAt);
        courseRepo.Verify(x => x.Update(It.IsAny<Course>()), Times.Never);
        uow.Verify(x => x.SaveChangesAsync(Ct), Times.Once);
    }

    [Fact]
    public async Task EnrollCourseSubjectStudentCommandHandler_ReturnsForbidden_WhenStudentNotEnrolledInCourse()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());

        var period = AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, period);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithFullGraphAsync(1, false, Ct)).ReturnsAsync(course);

        var uow = new Mock<IUnitOfWork>();

        var handler = new EnrollCourseSubjectStudentCommandHandler(adminScope.Object, uow.Object, courseRepo.Object);
        var result = await handler.Handle(new EnrollCourseSubjectStudentCommand(1, 10, 100), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("course.subject.student_not_enrolled", result.Error.Code);
    }

    [Fact]
    public async Task AssignCourseSubjectTeacherCommandHandler_ReturnsForbidden_WhenUserNotTeacherInCenter()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(1, Ct)).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.UserHasRoleInCenterAsync(100, 1, UserRole.Teacher, Ct)).ReturnsAsync(false);

        var period = AcademicPeriod.Create(1, "P1", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, period);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(x => x.GetWithFullGraphAsync(1, false, Ct)).ReturnsAsync(course);

        var uow = new Mock<IUnitOfWork>();

        var handler = new AssignCourseSubjectTeacherCommandHandler(adminScope.Object, uow.Object, courseRepo.Object);
        var result = await handler.Handle(new AssignCourseSubjectTeacherCommand(1, 10, 100), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("course.subject.teacher_not_eligible", result.Error.Code);
    }

    [Fact]
    public async Task AssignUserRoleCommandHandler_ReturnsForbidden_ForSuperAdmin()
    {
        var adminScope = new Mock<IAdminScopeService>();
        var userRepo = new Mock<IUserRepository>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new AssignUserRoleCommandHandler(adminScope.Object, uow.Object, userRepo.Object);
        var result = await handler.Handle(new AssignUserRoleCommand(1, 1, UserRole.SuperAdmin), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("userrole.superadmin_not_allowed", result.Error.Code);
    }

    [Fact]
    public async Task AssignUserRoleCommandHandler_ReturnsForbidden_WhenCenterNotInAdminScope()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(8, Ct))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var userRepo = new Mock<IUserRepository>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new AssignUserRoleCommandHandler(adminScope.Object, uow.Object, userRepo.Object);
        var result = await handler.Handle(new AssignUserRoleCommand(1, 8, UserRole.Teacher), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        adminScope.Verify(x => x.EnsureCanWriteUserAsync(It.IsAny<int>(), Ct), Times.Never);
    }

    [Fact]
    public async Task AssignUserRoleCommandHandler_ReturnsForbidden_WhenUserHasNoCenterInCommon()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(8, Ct)).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanWriteUserAsync(1, Ct))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var userRepo = new Mock<IUserRepository>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new AssignUserRoleCommandHandler(adminScope.Object, uow.Object, userRepo.Object);
        var result = await handler.Handle(new AssignUserRoleCommand(1, 8, UserRole.Teacher), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        userRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignUserRoleCommandHandler_AssignsRole_WhenUserSharesAnAdminCenter()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(8, Ct)).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanWriteUserAsync(1, Ct)).ReturnsAsync(Result.Success());

        var user = User.Create("user@example.com", "First", "Last");
        user.AssignRole(1, UserRole.Student);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetByIdAsync(1, Ct)).ReturnsAsync(user);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(Ct)).ReturnsAsync(1);

        var handler = new AssignUserRoleCommandHandler(adminScope.Object, uow.Object, userRepo.Object);
        var result = await handler.Handle(new AssignUserRoleCommand(1, 8, UserRole.Teacher), Ct);

        Assert.True(result.IsSuccess);
        Assert.Contains(user.RoleAssignments, x => x.CenterId == 8 && x.Role == UserRole.Teacher);
    }
}
