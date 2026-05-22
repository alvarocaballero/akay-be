using Akay.Be.Application.Features.LearningHubs.Responses;
using Akay.To.Core.Application.Abstractions.CognitiveServices;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

/// <summary>
/// Traduce el texto de introduccion a la economia a ingles y frances
/// usando Azure Cognitive Translator.
/// </summary>
public sealed record TranslateEconomicsTextQuery : IQuery<EconomicsTranslationResponse>;

internal sealed class TranslateEconomicsTextQueryHandler(ICognitiveTranslatorService translateService)
    : IQueryHandler<TranslateEconomicsTextQuery, EconomicsTranslationResponse>
{
    private static readonly string[] TargetLanguages = ["en", "fr"];

    public async ValueTask<Result<EconomicsTranslationResponse>> Handle(
        TranslateEconomicsTextQuery query,
        CancellationToken cancellationToken)
    {
        var result = await translateService.TranslateTextAsync(
            EconomicsText.Content,
            fromLanguage: null,
            TargetLanguages,
            cancellationToken);

        if (result.IsFailure)
            return Result<EconomicsTranslationResponse>.Failure(result.Error);

        var translation = result.Value!;
        var items = translation.Translations
            .Select(t => new EconomicsTranslationItem(t.Language, t.TranslatedText))
            .ToList();

        var preview = EconomicsText.Content.Length > 80
            ? EconomicsText.Content[..80] + "..."
            : EconomicsText.Content;

        return Result<EconomicsTranslationResponse>.Success(
            new EconomicsTranslationResponse(translation.DetectedLanguage, preview, items));
    }
}
