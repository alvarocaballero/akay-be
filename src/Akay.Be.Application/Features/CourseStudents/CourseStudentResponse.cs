namespace Akay.Be.Application.Features.CourseStudents;


public sealed record CourseStudentResponse(int CourseId,
                                           int UserId,
                                           int StudentCourseId,
                                           string? FirstName = null,
                                           string? LastName = null,
                                           string? Email = null);
