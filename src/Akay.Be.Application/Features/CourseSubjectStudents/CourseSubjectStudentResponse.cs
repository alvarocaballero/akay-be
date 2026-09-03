namespace Akay.Be.Application.Features.CourseSubjectStudents;

public sealed record CourseSubjectStudentResponse(int CourseId,
                                                   int SubjectId,
                                                   int UserId,
                                                   int StudentCourseId,
                                                  string? StudentNumber,
                                                  string? FirstName = null,
                                                  string? LastName = null,
                                                  string? Email = null);
