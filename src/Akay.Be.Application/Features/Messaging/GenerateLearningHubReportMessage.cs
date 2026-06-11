using Akay.To.Core.Application.Abstractions.Messaging;

namespace Akay.Be.Application.Features.Messaging;

public sealed record GenerateLearningHubReportMessage(int Id) : ICommandMessage;
