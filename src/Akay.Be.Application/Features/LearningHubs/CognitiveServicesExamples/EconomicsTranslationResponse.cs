namespace Akay.Be.Application.Features.LearningHubs.CognitiveServicesExamples;

public sealed record EconomicsTranslationResponse(
    string SourceLanguage,
    string OriginalTextPreview,
    List<EconomicsTranslationItem> Translations);

public sealed record EconomicsTranslationItem(
    string Language,
    string TranslatedText);
