namespace Akay.Be.Application.Features.LearningHubs.Responses;

public sealed record EconomicsTranslationResponse(
    string SourceLanguage,
    string OriginalTextPreview,
    List<EconomicsTranslationItem> Translations);

public sealed record EconomicsTranslationItem(
    string Language,
    string TranslatedText);
