namespace Akay.Be.Application.Features.Courses;

internal static class CourseMapper
{
    internal static CourseResponse ToResponse(Domain.Entities.Academic.Course course)
    {
        var subjects = course.Subjects
            .Select(s => new CourseSubjectResponse(
                s.SubjectId,
                s.Subject?.Name,
                s.Teachers.Select(t => t.UserId).ToList(),
                s.Students.Count))
            .ToList();

        return new CourseResponse(course.Id,
                                  course.AcademicPeriodId,
                                  course.AcademicPeriod.CenterId,
                                  course.Name,
                                  course.Code,
                                  subjects,
                                  course.Students.Count);
    }
}
