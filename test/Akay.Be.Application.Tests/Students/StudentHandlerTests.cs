using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.Students;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;

namespace Akay.Be.Application.Tests.Students;

public sealed class StudentHandlerTests
{
    [Fact]
    public async Task CreateStudent_SavesUserAndStudentOnce()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var studentRepository = new Mock<IStudentRepository>();
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.EmailExistsAsync("student@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var courseRepository = new Mock<ICourseRepository>();
        var handler = new CreateStudentCommandHandler(adminScope.Object,
                                                       unitOfWork.Object,
                                                       studentRepository.Object,
                                                       userRepository.Object,
                                                       courseRepository.Object);

        var result = await handler.Handle(new CreateStudentCommand(1,
                                                                   "S001",
                                                                   "student@example.com",
                                                                   "Ada",
                                                                   "Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        userRepository.Verify(x => x.Add(It.Is<User>(user => user.RoleAssignments.Single().CenterId == 1)), Times.Once);
        studentRepository.Verify(x => x.Add(It.Is<Student>(student => student.CenterId == 1 && student.User.Email == "student@example.com")), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteStudent_RemovesCourseAndSubjectEnrollmentsFromDomain()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteStudentAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var user = CreateUser(100);
        var student = Student.Create(user, 1, "S001");
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        course.EnrollStudent(student, [10]);

        var studentRepository = new Mock<IStudentRepository>();
        studentRepository.Setup(x => x.GetByUserIdAndCenterIdAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync(student);

        var courseRepository = new Mock<ICourseRepository>();
        courseRepository.Setup(x => x.GetByStudentForUpdateAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync([course]);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteStudentCommandHandler(adminScope.Object,
                                                       unitOfWork.Object,
                                                       studentRepository.Object,
                                                       courseRepository.Object);

        var result = await handler.Handle(new DeleteStudentCommand(100, 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(course.Students);
        Assert.Empty(course.Subjects.Single().Students);
        studentRepository.Verify(x => x.Remove(student), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static User CreateUser(int id)
    {
        var user = User.Create($"student{id}@example.com", "Student", "Test");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }
}
