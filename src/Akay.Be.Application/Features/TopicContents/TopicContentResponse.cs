using Akay.Be.Domain.Enums;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record TopicContentResponse(int Id,
                                          int TopicId,
                                          string FileName,
                                          string ContentType,
                                          string? OriginalContentUrl,
                                          string? ContentUrl,
                                          ContentProcessingState ProcessingState,
                                          ContentIndexingState IndexingState,
                                          string? ProcessingError,
                                          string? IndexingError,
                                          int ProcessingAttemptId);
