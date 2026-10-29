using Akay.Be.Domain.Enums;

namespace Akay.Be.Application.Features.Topics;

public sealed record TopicResponse(int Id,
                                   int SubjectId,
                                   string Name,
                                   string? Description,
                                   int DisplayOrder,
                                   TopicProcessingState ProcessingState,
                                   TopicIndexingState IndexingState,
                                   bool IsActive);
