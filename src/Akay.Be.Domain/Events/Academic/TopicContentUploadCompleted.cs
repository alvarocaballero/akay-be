using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Academic;

public sealed record TopicContentUploadCompleted(int SubjectId, int TopicId, int ContentId, int UserId,
                                                 Guid RequestId, string FileName, string SourceUrl) : IDomainEvent;
