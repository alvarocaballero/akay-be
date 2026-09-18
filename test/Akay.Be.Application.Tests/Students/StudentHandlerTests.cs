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

    [Fact]
    public async Task ImportStudentsCsv_CreatesStudentsAndEnrollsInAllCourseSubjects()
    {
        var (handler, userRepository, studentRepository, persistence, course) = CreateImportFixture();

        var existingUser = CreateExistingUser(50, "existing@example.com", "Old", "Name");
        userRepository.Setup(x => x.GetByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync([existingUser]);
        studentRepository.Setup(x => x.GetByUserIdsForCenterAsync(It.IsAny<IEnumerable<int>>(), 1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync([]);
        course.AddSubject(10);
        course.AddSubject(20);

        var csv = """
                  Email,StudentNumber,FirstName,LastName
                  ada@example.com,S001,Ada,Lovelace
                  alan@example.com,S002,Alan,Turing
                  existing@example.com,S003,Existing,User
                  ada@example.com,S004,Duplicate,Row
                  bad-email,S005,Bad,Email
                  """;

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, csv), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ImportedCount);
        Assert.Equal(1, result.Value!.UpdatedCount);
        Assert.Equal(0, result.Value!.SkippedCount);
        Assert.Equal(2, result.Value!.FailedCount);
        Assert.Equal([5, 6], result.Value!.Errors.Select(error => error.Row));
        Assert.Equal("Existing", existingUser.FirstName);
        Assert.Equal("User", existingUser.LastName);
        Assert.Equal(3, course.Students.Count);
        Assert.All(course.Subjects, subject => Assert.Equal(3, subject.Students.Count));
        userRepository.Verify(x => x.GetByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        persistence.Verify(x => x.TrySaveChangesAsync(It.IsAny<IReadOnlyDictionary<string, Error>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportStudentsCsv_ExistingStudentAlreadyEnrolledWithSameData_IsSkippedWithoutFailure()
    {
        var (handler, userRepository, studentRepository, _, course) = CreateImportFixture();

        var existingUser = CreateExistingUser(50, "ada@example.com", "Ada", "Lovelace");
        var existingStudent = Domain.Entities.Academic.Student.Create(existingUser, 1, "S001");
        course.EnrollStudent(existingStudent, null);

        userRepository.Setup(x => x.GetByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync([existingUser]);
        studentRepository.Setup(x => x.GetByUserIdsForCenterAsync(It.IsAny<IEnumerable<int>>(), 1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync([existingStudent]);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,StudentNumber,FirstName,LastName\r\nada@example.com,S001,Ada,Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ImportedCount);
        Assert.Equal(0, result.Value!.UpdatedCount);
        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal(0, result.Value!.FailedCount);
        Assert.Single(course.Students);
    }

    [Fact]
    public async Task ImportStudentsCsv_ExistingStudentFromAnotherCourse_UpdatesAndEnrollsInCourse()
    {
        var (handler, userRepository, studentRepository, _, course) = CreateImportFixture();

        var existingUser = CreateExistingUser(50, "ada@example.com", "Ada", "Lovelace");
        var existingStudent = Domain.Entities.Academic.Student.Create(existingUser, 1, "S001");

        userRepository.Setup(x => x.GetByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync([existingUser]);
        studentRepository.Setup(x => x.GetByUserIdsForCenterAsync(It.IsAny<IEnumerable<int>>(), 1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync([existingStudent]);
        course.AddSubject(10);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,StudentNumber,FirstName,LastName\r\nada@example.com,S999,Ada,Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ImportedCount);
        Assert.Equal(1, result.Value!.UpdatedCount);
        Assert.Equal("S999", existingStudent.StudentNumber);
        Assert.Single(course.Students);
        Assert.Single(course.Subjects.Single().Students);
    }

    [Fact]
    public async Task ImportStudentsCsv_InvalidHeader_ReturnsValidation()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, AcademicPeriod.Create(1, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30)));

        var courseRepository = new Mock<ICourseRepository>();
        courseRepository.Setup(x => x.GetWithFullGraphAsync(5, false, It.IsAny<CancellationToken>())).ReturnsAsync(course);

        var handler = new ImportStudentsCsvCommandHandler(adminScope.Object,
                                                           new Mock<IPersistenceResultExecutor>().Object,
                                                           new Mock<IStudentRepository>().Object,
                                                           new Mock<IUserRepository>().Object,
                                                           courseRepository.Object);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Mail,Nombre\r\nada@example.com,Ada"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("csv.invalid_header", result.Error!.Code);
    }

    [Fact]
    public async Task ImportStudentsCsv_MalformedCsv_ReturnsValidation()
    {
        var handler = CreateImportHandler();

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,FirstName,LastName\r\n\"A\"da,First,Last"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("csv.malformed", result.Error!.Code);
    }

    [Fact]
    public async Task ImportStudentsCsv_FieldTooLong_ReportsRowErrorAndSkipsPersistence()
    {
        var (handler, _, _, persistence, _) = CreateImportFixture();
        var longFirstName = new string('a', 101);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1,
                                                                       5,
                                                                       $"Email,StudentNumber,FirstName,LastName\r\nada@example.com,S001,{longFirstName},Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("csv.no_valid_rows", result.Error!.Code);
        persistence.Verify(x => x.TrySaveChangesAsync(It.IsAny<IReadOnlyDictionary<string, Error>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportStudentsCsv_CenterDenied_ReturnsForbiddenWithoutWrites()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(1, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Error.Forbidden("center.denied", "No eres admin del centro."));

        var studentRepository = new Mock<IStudentRepository>();
        var userRepository = new Mock<IUserRepository>();
        var persistence = new Mock<IPersistenceResultExecutor>();
        var handler = new ImportStudentsCsvCommandHandler(adminScope.Object,
                                                           persistence.Object,
                                                           studentRepository.Object,
                                                           userRepository.Object,
                                                           new Mock<ICourseRepository>().Object);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,FirstName,LastName\r\nada@example.com,Ada,Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("center.denied", result.Error!.Code);
        studentRepository.Verify(x => x.Add(It.IsAny<Domain.Entities.Academic.Student>()), Times.Never);
        userRepository.Verify(x => x.Add(It.IsAny<User>()), Times.Never);
        persistence.Verify(x => x.TrySaveChangesAsync(It.IsAny<IReadOnlyDictionary<string, Error>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportStudentsCsv_CourseFromAnotherCenter_ReturnsForbidden()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, AcademicPeriod.Create(2, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30)));

        var courseRepository = new Mock<ICourseRepository>();
        courseRepository.Setup(x => x.GetWithFullGraphAsync(5, false, It.IsAny<CancellationToken>())).ReturnsAsync(course);

        var persistence = new Mock<IPersistenceResultExecutor>();
        var handler = new ImportStudentsCsvCommandHandler(adminScope.Object,
                                                           persistence.Object,
                                                           new Mock<IStudentRepository>().Object,
                                                           new Mock<IUserRepository>().Object,
                                                           courseRepository.Object);

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,FirstName,LastName\r\nada@example.com,Ada,Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("course.student_wrong_center", result.Error!.Code);
        persistence.Verify(x => x.TrySaveChangesAsync(It.IsAny<IReadOnlyDictionary<string, Error>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ImportStudentsCsvCommandHandler CreateImportHandler()
    {
        var (handler, _, _, _, _) = CreateImportFixture();
        return handler;
    }

    private static (ImportStudentsCsvCommandHandler Handler,
                    Mock<IUserRepository> UserRepository,
                    Mock<IStudentRepository> StudentRepository,
                    Mock<IPersistenceResultExecutor> Persistence,
                    Course Course) CreateImportFixture()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureAdminOfCenterAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanWriteCourseAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var course = Course.Create(1, "1º ESO", "ESO1");
        typeof(Course).GetProperty(nameof(Course.AcademicPeriod))!.SetValue(course, AcademicPeriod.Create(1, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30)));

        var courseRepository = new Mock<ICourseRepository>();
        courseRepository.Setup(x => x.GetWithFullGraphAsync(5, false, It.IsAny<CancellationToken>())).ReturnsAsync(course);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync([]);

        var studentRepository = new Mock<IStudentRepository>();
        studentRepository.Setup(x => x.GetByUserIdsForCenterAsync(It.IsAny<IEnumerable<int>>(), 1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync([]);

        var persistence = new Mock<IPersistenceResultExecutor>();
        persistence.Setup(x => x.TrySaveChangesAsync(It.IsAny<IReadOnlyDictionary<string, Error>?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Result.Success());

        var handler = new ImportStudentsCsvCommandHandler(adminScope.Object,
                                                          persistence.Object,
                                                          studentRepository.Object,
                                                          userRepository.Object,
                                                          courseRepository.Object);

        return (handler, userRepository, studentRepository, persistence, course);
    }

    [Fact]
    public async Task ImportStudentsCsv_MapsSqlGenericConstraintKeys_KnownBySqlServerProvider()
    {
        var (handler, _, _, persistence, _) = CreateImportFixture();

        var result = await handler.Handle(new ImportStudentsCsvCommand(1, 5, "Email,FirstName,LastName\r\nada@example.com,Ada,Lovelace"),
                                          TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        persistence.Verify(x => x.TrySaveChangesAsync(It.Is<IReadOnlyDictionary<string, Error>?>(known =>
                        known != null
                        && known.ContainsKey("sql.unique_violation")
                        && known.ContainsKey("sql.not_null_violation")
                        && known.ContainsKey("sql.foreign_key_violation")
                        && known["sql.unique_violation"].Code == "user.email_exists"),
                    It.IsAny<CancellationToken>()),
                    Times.Once);
    }

    private static User CreateUser(int id)
    {
        var user = User.Create($"student{id}@example.com", "Student", "Test");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    private static User CreateExistingUser(int id, string email, string firstName, string lastName)
    {
        var user = User.Create(email, firstName, lastName);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }
}
