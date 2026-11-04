using Akay.Be.Domain.Enums;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record TopicContentResponse(int Id,
                                          int TopicId,
                                          string FileName,
                                          string ContentType,
                                          string? SourceContentUrl,
                                          string? ContentUrl,
                                          ContentProcessingState ProcessingState,
                                          ContentIndexingState IndexingState,
                                          string? ProcessingError,
                                          string? IndexingError,
                                          Guid? RequestId);
