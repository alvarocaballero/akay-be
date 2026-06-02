using System.Text;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.BlobStorageExamples;

/// <summary>
/// Query que genera (o recupera desde caché de blob) un badge SVG para un Learning Hub.
/// </summary>
/// <remarks>
/// Implementa <see cref="IBlobCacheable{string}"/> para activar el behavior de caché en blob storage del mediator.
/// Esto significa que, si el archivo ya existe en el contenedor configurado, el pipeline devuelve la URI existente
/// sin volver a ejecutar el handler (short-circuit). Si no existe, se ejecuta el handler, se genera el SVG y se sube.
/// </remarks>
public sealed record GetLearningHubBadgeUriQuery(int Id, bool ForceRegenerate = false) : IQuery<string>, IBlobCacheable<string>
{
    /// <summary>
    /// Nombre del contenedor de Azure Blob Storage donde se almacenará el archivo. Ejemplo: "additional-content-pdfs",
    /// "reports", "invoices", etc.
    /// </summary>
    public string BlobContainerName => "additional-content-pdfs";

    /// <summary>
    /// Nombre (ruta) del blob dentro del contenedor. Actúa como clave de caché.
    /// Se recomienda incluir el identificador de la entidad y la versión o tipo de contenido
    /// para evitar colisiones y facilitar la invalidación. Ejemplo: "hubs/{id}/badge.svg".
    /// </summary>
    public string BlobName => $"hubs/{Id}/badge.svg";

    /// <summary>
    /// Cuando es <c>true</c>, fuerza la regeneración del archivo aunque ya exista en blob storage.
    /// Útil para: cambios de plantilla, branding, warm-up manual o regeneración bajo demanda.
    /// Se controla desde el endpoint con el parámetro de query <c>?forceRegenerate=true</c>.
    /// </summary>
    public bool BypassBlobCache => ForceRegenerate;

    /// <summary>
    /// Construye el valor de respuesta cuando el behavior detecta un hit en caché (el blob ya existe).
    /// Devuelve la URI pública del blob para que el cliente pueda acceder al archivo directamente.
    /// </summary>
    public string CreateCachedValue(Uri blobUri) => blobUri.ToString();
}

internal sealed class GetLearningHubBadgeUriQueryHandler(IBlobStorageServiceFactory blobFactory) : IQueryHandler<GetLearningHubBadgeUriQuery, string>
{
    public async ValueTask<Result<string>> Handle(GetLearningHubBadgeUriQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hub = LearningHubStore.GetById(request.Id);
        if (hub is null)
            return Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado.");

        // Obtiene (o crea si no existe) el contenedor de blob storage configurado en la query.
        var blob = await blobFactory.CreateAsync(request.BlobContainerName, forceCreateContainer: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Genera el contenido del archivo (en este caso un SVG) en memoria.
        var svg = BuildBadgeSvg(hub.Name, hub.Category);
        var bytes = Encoding.UTF8.GetBytes(svg);
        using var stream = new MemoryStream(bytes);

        // UploadOrGetUriAsync intenta subir el blob con overwrite:false.
        // - Si sube correctamente: devuelve la URI del nuevo blob.
        // - Si ya existe (409 Conflict): devuelve la URI del blob existente sin error.
        // Esto resuelve carreras concurrentes de forma idempotente y sin locks globales.
        var uri = await blob.UploadOrGetUriAsync(request.BlobName,
                                                 stream,
                                                 contentType: "image/svg+xml",
                                                 compress: false,
                                                 cancellationToken: cancellationToken).ConfigureAwait(false);

        return uri;
    }

    private static string BuildBadgeSvg(string name, string category) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"240\" viewBox=\"0 0 800 240\"><rect width=\"800\" height=\"240\" fill=\"#0f172a\"/><rect x=\"18\" y=\"18\" width=\"764\" height=\"204\" rx=\"18\" fill=\"#111827\" stroke=\"#38bdf8\" stroke-width=\"2\"/><text x=\"40\" y=\"95\" fill=\"#e2e8f0\" font-size=\"38\" font-family=\"Segoe UI\">{EscapeXml(name)}</text><text x=\"40\" y=\"145\" fill=\"#94a3b8\" font-size=\"22\" font-family=\"Segoe UI\">Category: {EscapeXml(category)}</text></svg>";

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal)
             .Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("'", "&apos;", StringComparison.Ordinal);
}
