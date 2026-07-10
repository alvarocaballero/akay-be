namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record AcademicPeriodResponse(int Id,
                                            int CenterId,
                                            string Name,
                                            DateOnly StartDate,
                                            DateOnly EndDate,
                                            bool IsActive);
