namespace Akay.Be.Application.Features.Courses;

public sealed record CourseResponse(int Id,
                                    int AcademicPeriodId,
                                    int CenterId,
                                    string Name,
                                    string Code,
                                    IReadOnlyList<CourseSubjectResponse> Subjects,
                                    int StudentCount);

public sealed record CourseSubjectResponse(int SubjectId,
                                           string SubjectName,
                                           IReadOnlyList<int> TeacherIds,
                                           int StudentCount);

public sealed record CourseListResponse(int Id,
                                        int AcademicPeriodId,
                                        int CenterId,
                                        string Name,
                                        string Code);

