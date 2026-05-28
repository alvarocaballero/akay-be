using System.Runtime.CompilerServices;
using Akay.To.Core.Application.Abstractions.CognitiveServices;
using Akay.To.Core.Application.Abstractions.Mediator;
using Microsoft.Extensions.Logging;

namespace Akay.Be.Application.Features.LearningHubs;

/// <summary>
/// Genera un stream de audio (TTS) a partir del texto de introduccion a la economia
/// usando Azure Cognitive Speech. El audio se devuelve en chunks de bytes.
/// El resultado se cachea en blob storage para evitar regeneracion.
/// </summary>
public sealed record SpeechEconomicsTextRequest : IStreamRequest<byte[]>;

internal sealed class SpeechEconomicsTextRequestHandler(
    ICognitiveSpeechService speechService,
    ILogger<SpeechEconomicsTextRequestHandler> logger)
    : IStreamRequestHandler<SpeechEconomicsTextRequest, byte[]>
{
    private const string BlobContainer = "economics-audio";
    private const string AudioFileName = "intro-economics";

    public async IAsyncEnumerable<byte[]> Handle(
        SpeechEconomicsTextRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chunk in speechService.TextToSpeechCacheableStreamAsync(
                           EconomicsText.Content,
                           BlobContainer,
                           AudioFileName,
                           cancellationToken))
        {
            if (chunk.IsSuccess)
            {
                yield return chunk.Value!;
            }
            else
            {
                logger.LogWarning(
                    "Speech generation warning: {Code} - {Description}",
                    chunk.Error.Code,
                    chunk.Error.Description);
            }
        }
    }
}
