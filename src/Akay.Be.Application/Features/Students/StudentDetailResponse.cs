namespace Akay.Be.Application.Features.Students;

public sealed record StudentDetailResponse(int UserId,
                                           int CenterId,
                                           string? StudentNumber,
                                           bool IsActive,
                                           string? FirstName,
                                           string? LastName,
                                           string? Email,
                                           List<EnrolledCourseResponse> Courses);

public sealed record EnrolledCourseResponse(int CourseId,
                                            string CourseName,
                                            string CourseCode,
                                            List<EnrolledSubjectResponse> Subjects);

public sealed record EnrolledSubjectResponse(int SubjectId,
                                             string SubjectName);
