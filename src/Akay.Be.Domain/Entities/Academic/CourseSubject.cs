using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class CourseSubject : Entity<int>, IAuditable, ISoftDeletable
{
    private readonly List<CourseSubjectTeacher> _teachers = [];
    private readonly List<CourseSubjectStudent> _students = [];

    private CourseSubject() { }

    public int CourseId { get; private set; }
    public Course Course { get; private set; } = default!;
    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<CourseSubjectTeacher> Teachers => _teachers.AsReadOnly();
    public IReadOnlyCollection<CourseSubjectStudent> Students => _students.AsReadOnly();

    internal static CourseSubject Create(int courseId, int subjectId)
    {
        if (subjectId <= 0)
            throw new ArgumentException("SubjectId must be greater than zero.", nameof(subjectId));

        return new CourseSubject
        {
            CourseId = courseId,
            SubjectId = subjectId
        };
    }

    public void AssignTeacher(int userId)
    {
        if (_teachers.Any(t => t.UserId == userId))
            throw new InvalidOperationException($"User {userId} is already assigned as teacher to this course subject.");

        var teacher = CourseSubjectTeacher.Create(Id, userId);
        _teachers.Add(teacher);
    }

    public bool EnrollStudent(StudentCourse studentCourse)
    {
        ArgumentNullException.ThrowIfNull(studentCourse);

        if (_students.Any(s => studentCourse.Id > 0 && s.StudentCourseId == studentCourse.Id
                               || ReferenceEquals(s.StudentCourse, studentCourse)))
            return false;

        _students.Add(CourseSubjectStudent.Create(studentCourse));
        return true;
    }

    internal void UnenrollStudent(StudentCourse studentCourse)
    {
        ArgumentNullException.ThrowIfNull(studentCourse);

        _students.RemoveAll(s => studentCourse.Id > 0 && s.StudentCourseId == studentCourse.Id
                                 || ReferenceEquals(s.StudentCourse, studentCourse));
    }

}
