using Akay.Be.Application.Features.LearningHubs;
using Akay.Be.Application.Features.LearningHubs.Responses;
using Akay.To.Core.Application.Abstractions.CognitiveServices;
using Akay.To.Core.Application.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Akay.Be.Application.Tests;

public sealed class TranslateEconomicsTextQueryHandlerTests
{
    [Fact]
    public async Task Handle_success_returns_translations()
    {
        var translationResult = new TranslationResult(
            "es",
            [
                new TranslationItem("en", "Economics is a social science..."),
                new TranslationItem("fr", "L'economie est une science sociale...")
            ]);

        var mockTranslate = new Mock<ICognitiveTranslatorService>();
        mockTranslate
            .Setup(s => s.TranslateTextAsync(
                EconomicsText.Content,
                null,
                It.Is<IReadOnlyCollection<string>>(languages => languages.Count == 2 &&
                                             languages.Contains("en") &&
                                             languages.Contains("fr")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranslationResult>.Success(translationResult));

        var handler = new TranslateEconomicsTextQueryHandler(mockTranslate.Object);

        var result = await handler.Handle(new TranslateEconomicsTextQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("es", result.Value!.SourceLanguage);
        Assert.Equal(2, result.Value.Translations.Count);
        Assert.Contains(result.Value.Translations, t => t.Language == "en");
        Assert.Contains(result.Value.Translations, t => t.Language == "fr");
        Assert.Contains("...", result.Value.OriginalTextPreview);
    }

    [Fact]
    public async Task Handle_translate_failure_propagates_error()
    {
        var expectedError = Error.Unavailable("translator.down", "Service unavailable");
        var mockTranslate = new Mock<ICognitiveTranslatorService>();
        mockTranslate
            .Setup(s => s.TranslateTextAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranslationResult>.Failure(expectedError));

        var handler = new TranslateEconomicsTextQueryHandler(mockTranslate.Object);

        var result = await handler.Handle(new TranslateEconomicsTextQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("translator.down", result.Error.Code);
        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    [Fact]
    public async Task Handle_long_text_truncates_preview()
    {
        var translationResult = new TranslationResult("es", [new TranslationItem("en", "Hi")]);
        var mockTranslate = new Mock<ICognitiveTranslatorService>();
        mockTranslate
            .Setup(s => s.TranslateTextAsync(
                It.IsAny<string>(), null,
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranslationResult>.Success(translationResult));

        var handler = new TranslateEconomicsTextQueryHandler(mockTranslate.Object);

        var result = await handler.Handle(new TranslateEconomicsTextQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // EconomicsText.Content is > 80 chars, preview should be truncated with "..."
        Assert.EndsWith("...", result.Value!.OriginalTextPreview);
        Assert.Equal(83, result.Value.OriginalTextPreview.Length);
    }
}

public sealed class SpeechEconomicsTextRequestHandlerTests
{
    [Fact]
    public async Task Handle_yields_success_chunks()
    {
        var chunk1 = new byte[] { 1, 2, 3 };
        var chunk2 = new byte[] { 4, 5, 6 };

        var mockSpeech = new Mock<ICognitiveSpeechService>();
        mockSpeech
            .Setup(s => s.TextToSpeechCacheableStreamAsync(
                EconomicsText.Content,
                "economics-audio",
                "intro-economics",
                It.IsAny<CancellationToken>()))
            .Returns(new Result<byte[]>[]
            {
                Result<byte[]>.Success(chunk1),
                Result<byte[]>.Success(chunk2)
            }.ToAsyncEnumerable());

        var handler = new SpeechEconomicsTextRequestHandler(
            mockSpeech.Object,
            NullLogger<SpeechEconomicsTextRequestHandler>.Instance);

        var results = new List<byte[]>();
        await foreach (var chunk in handler.Handle(
                           new SpeechEconomicsTextRequest(), CancellationToken.None))
        {
            results.Add(chunk);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(chunk1, results[0]);
        Assert.Equal(chunk2, results[1]);
    }

    [Fact]
    public async Task Handle_skips_failure_chunks_and_logs_warning()
    {
        var logger = new TestLogger<SpeechEconomicsTextRequestHandler>();
        var failure = Result<byte[]>.Failure(Error.Unavailable("speech.error", "fail"));

        var mockSpeech = new Mock<ICognitiveSpeechService>();
        mockSpeech
            .Setup(s => s.TextToSpeechCacheableStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(new Result<byte[]>[] { failure }.ToAsyncEnumerable());

        var handler = new SpeechEconomicsTextRequestHandler(mockSpeech.Object, logger);

        var results = new List<byte[]>();
        await foreach (var chunk in handler.Handle(
                           new SpeechEconomicsTextRequest(), CancellationToken.None))
        {
            results.Add(chunk);
        }

        Assert.Empty(results);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
                                              e.Message.Contains("speech.error"));
    }

    [Fact]
    public async Task Handle_mixed_success_and_failure_yields_only_success()
    {
        var successChunk = new byte[] { 1, 2, 3 };
        var failure = Result<byte[]>.Failure(Error.Unavailable("speech.error", "fail"));

        var mockSpeech = new Mock<ICognitiveSpeechService>();
        mockSpeech
            .Setup(s => s.TextToSpeechCacheableStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(new Result<byte[]>[]
            {
                Result<byte[]>.Success(successChunk),
                failure,
                Result<byte[]>.Success(successChunk)
            }.ToAsyncEnumerable());

        var handler = new SpeechEconomicsTextRequestHandler(
            mockSpeech.Object,
            NullLogger<SpeechEconomicsTextRequestHandler>.Instance);

        var results = new List<byte[]>();
        await foreach (var chunk in handler.Handle(
                           new SpeechEconomicsTextRequest(), CancellationToken.None))
        {
            results.Add(chunk);
        }

        Assert.Equal(2, results.Count);
    }
}

// ── Helpers ───────────────────────────────────────────────────────────

internal sealed class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

internal static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
            yield return item;
    }
}
