namespace Akay.Be.Application.Features.TopicAdditionalContents;

public sealed record TopicAdditionalContentResponse(int Id,
                                                    int TopicId,
                                                    string FileName,
                                                    string ContentType,
                                                    string? SourceContentUrl,
                                                    string? ContentUrl,
                                                    bool? IsVisible);
