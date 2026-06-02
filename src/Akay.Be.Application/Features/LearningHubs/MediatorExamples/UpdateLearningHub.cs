using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.LearningHubs.MediatorExamples;

public sealed record UpdateLearningHubRequest(string Name,
                                              string Description,
                                              string Address,
                                              string Category);

public sealed record UpdateLearningHubCommand(int Id, UpdateLearningHubRequest Request) : ICommand;

internal sealed class UpdateLearningHubCommandHandler : ICommandHandler<UpdateLearningHubCommand>
{
    public async ValueTask<Result> Handle(UpdateLearningHubCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = LearningHubStore.GetById(command.Id);

        if (existing is null)
            return Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {command.Id} no encontrado.");

        var updated = existing with
        {
            Name = command.Request.Name,
            Description = command.Request.Description,
            Address = command.Request.Address,
            Category = command.Request.Category
        };

        var success = LearningHubStore.Update(updated);

        return success
            ? Result.Success()
            : Error.Failure("learninghub.update_failed", "No se pudo actualizar el centro de estudios.");
    }
}

public sealed class UpdateLearningHubCommandValidator : AbstractValidator<UpdateLearningHubCommand>
{
    public UpdateLearningHubCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.Address).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Category).NotEmpty().MaximumLength(50);
    }
}
