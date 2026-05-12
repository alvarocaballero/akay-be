using Akay.To.Core.Application.Mediator;
using Akay.To.Core.Application.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record CreateLearningHubCommand(string Name,
                                              string Description,
                                              string Address,
                                              string Category) : ICommand<LearningHubResponse>;

internal sealed class CreateLearningHubCommandHandler(IOptions<ApplicationSettings> settings) : ICommandHandler<CreateLearningHubCommand, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(CreateLearningHubCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exists = LearningHubStore.GetAll()
                                     .Any(h => string.Equals(h.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        if (exists)
            return ValueTask.FromResult<Result<LearningHubResponse>>(Error.Conflict("learninghub.duplicate", $"Ya existe un centro con el nombre '{request.Name} en {settings.Value.Application.Name}'."));


        var data = new LearningHubData(0,
                                       request.Name,
                                       request.Description,
                                       request.Address,
                                       request.Category,
                                       "active",
                                       DateTime.MinValue,
                                       DateTime.MinValue);

        var created = LearningHubStore.Add(data);

        var response = new LearningHubResponse(created.Id,
                                               created.Name,
                                               created.Description,
                                               created.Address,
                                               created.Category,
                                               created.Status,
                                               created.CreatedAt,
                                               created.UpdatedAt);

        return ValueTask.FromResult<Result<LearningHubResponse>>(response);
    }
}

public sealed class CreateLearningHubCommandValidator : AbstractValidator<CreateLearningHubCommand>
{
    public CreateLearningHubCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
    }
}
