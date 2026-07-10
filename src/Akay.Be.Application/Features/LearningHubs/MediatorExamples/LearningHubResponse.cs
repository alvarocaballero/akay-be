namespace Akay.Be.Application.Features.LearningHubs.MediatorExamples;

public sealed record LearningHubResponse(int Id,
                                         string Name,
                                         string Description,
                                         string Address,
                                         string Category,
                                         string Status);
