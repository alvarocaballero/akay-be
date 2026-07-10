namespace Akay.Be.Application.Features.CourseSubjectTeachers;

public sealed record CourseSubjectTeacherResponse(int CourseId,
                                                  int SubjectId,
                                                  int UserId,
                                                  string? FirstName = null,
                                                  string? LastName = null,
                                                  string? Email = null);
