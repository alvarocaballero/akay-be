using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class Course : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<CourseSubject> _subjects = [];
    private readonly List<StudentCourse> _students = [];

    private Course() { }

    public int AcademicPeriodId { get; private set; }
    public AcademicPeriod AcademicPeriod { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<CourseSubject> Subjects => _subjects.AsReadOnly();
    public IReadOnlyCollection<StudentCourse> Students => _students.AsReadOnly();

    public static Course Create(int academicPeriodId, string name, string code)
    {
        if (academicPeriodId <= 0)
            throw new ArgumentException("AcademicPeriodId must be greater than zero.", nameof(academicPeriodId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new Course
        {
            AcademicPeriodId = academicPeriodId,
            Name = name,
            Code = code
        };
    }

    public void AddSubject(int subjectId)
    {
        if (_subjects.Any(s => s.SubjectId == subjectId))
            throw new InvalidOperationException($"Subject {subjectId} is already assigned to this course.");

        var courseSubject = CourseSubject.Create(Id, subjectId);
        _subjects.Add(courseSubject);
    }

    public void EnrollStudent(int userId)
    {
        if (_students.Any(s => s.UserId == userId))
            throw new InvalidOperationException($"User {userId} is already enrolled in this course.");

        var studentCourse = StudentCourse.Create(Id, userId);
        _students.Add(studentCourse);
    }

    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void UpdateCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

}
