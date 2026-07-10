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
        if (_teachers.Any(t => t.UserId == userId && t.DeletedAt == null))
            throw new InvalidOperationException($"User {userId} is already assigned as teacher to this course subject.");

        var teacher = CourseSubjectTeacher.Create(Id, userId);
        _teachers.Add(teacher);
    }

    public void RemoveTeacher(int userId)
    {
        var teacher = _teachers.FirstOrDefault(t => t.UserId == userId && t.DeletedAt == null)
            ?? throw new InvalidOperationException($"User {userId} is not assigned as teacher to this course subject.");

        teacher.SoftDelete();
    }

    public void EnrollStudent(int studentCourseId)
    {
        if (_students.Any(s => s.StudentCourseId == studentCourseId && s.DeletedAt == null))
            throw new InvalidOperationException($"StudentCourse {studentCourseId} is already enrolled in this course subject.");

        var enrollment = CourseSubjectStudent.Create(Id, studentCourseId);
        _students.Add(enrollment);
    }

    public void UnenrollStudent(int studentCourseId)
    {
        var enrollment = _students.FirstOrDefault(s => s.StudentCourseId == studentCourseId && s.DeletedAt == null)
            ?? throw new InvalidOperationException($"StudentCourse {studentCourseId} is not enrolled in this course subject.");

        enrollment.SoftDelete();
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
