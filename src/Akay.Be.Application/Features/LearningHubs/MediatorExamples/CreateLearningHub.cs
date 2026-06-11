using System.Diagnostics;
using System.Globalization;
using Akay.Be.Application.Features.Messaging;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Application.Contexts;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.LearningHubs.MediatorExamples;

/// <summary>
/// Comando que crea un nuevo Learning Hub con sus datos básicos y un archivo asociado.
/// </summary>
/// <remarks>
/// Implementa <see cref="IRetryableRequest"/> para que el pipeline aplique retry automático
/// si el handler lanza una excepción transitoria (ej. timeout de red al subir blob).
/// Implementa <see cref="ICompensableRequest"/> para registrar acciones de compensación
/// que se ejecutan automáticamente si algo falla después de la creación, garantizando consistencia.
/// </remarks>
public sealed record CreateLearningHubCommand(string Name,
                                              string Description,
                                              string Address,
                                              string Category,
                                              Stream FileStream,
                                              string FileName,
                                              string ContentType,
                                              int FailedAttempts = 0) : ICommand<LearningHubResponse>, IRetryableRequest, ICompensableRequest
{
    /// <summary>
    /// Número de reintentos que el <see cref="RetryBehavior"/> aplicará ante excepciones transitorias.
    /// Valor por defecto: 3 intentos (1 original + 2 reintentos).
    /// </summary>
    public int RetryCount => 3;

    /// <summary>
    /// Retardo base entre reintentos. El behavior aplica backoff exponencial a partir de este valor.
    /// </summary>
    public TimeSpan BaseDelay => TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Handler que procesa la creación de un Learning Hub, incluyendo validación de duplicados,
/// persistencia en memoria, subida de archivo a blob storage y notificación simulada.
/// </summary>
internal sealed class CreateLearningHubCommandHandler(ICompensationContext compensations,
                                                      IMessageBus messageBus,
                                                      IBlobStorageServiceFactory blobFactory,
                                                      IUserContext userContext) : ICommandHandler<CreateLearningHubCommand, LearningHubResponse>
{
    public async ValueTask<Result<LearningHubResponse>> Handle(CreateLearningHubCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validación de negocio: evitar duplicados por nombre (case-insensitive)
        var exists = LearningHubStore.GetAll().Any(h => string.Equals(h.Name, request.Name, StringComparison.OrdinalIgnoreCase));
        if (exists)
            return Error.Conflict("learninghub.duplicate", $"Ya existe un centro con el nombre '{request.Name}'.");

        // 2. Crear la entidad en el store en memoria (ejemplo didáctico)
        var data = new LearningHubData(0,
                                       request.Name,
                                       request.Description,
                                       request.Address,
                                       request.Category,
                                       "active",
                                       DateTime.MinValue,
                                       DateTime.MinValue);

        var created = LearningHubStore.Add(data);

        // 3. Registrar compensación: si algo falla más adelante, se elimina el hub creado.
        // Esto asegura que no quede "basura" en el store si la subida del blob o la notificación fallan.
        compensations.Add(() => DeleteHubAsync(created.Id), $"Delete created hub '{created.Name}'");

        // 4. Obtener servicio de blob storage para el contenedor "uploadchatdocument".
        // Parámetros:
        //   - containerName: nombre del contenedor en Azure Blob Storage.
        //   - forceCreateContainer: true => crea el contenedor si no existe.
        //   - compressContainer: true => por defecto comprime todos los blobs de este contenedor.
        var blob = await blobFactory.CreateAsync("uploadchatdocument", forceCreateContainer: true, compressContainer: true, cancellationToken: cancellationToken);

        // 5. Construir la ruta del blob: {id}/files/{nombreArchivo}
        // Esto organiza los archivos por entidad y evita colisiones de nombres.
        var blobName = $"{created.Id}/files/{request.FileName}";

        // 6. Registrar compensación para el blob: si falla algo posterior, se borra el archivo subido.
        compensations.Add(() => blob.DeleteAsync(blobName, CancellationToken.None), $"Delete blob '{blobName}'");

        // 7. Subir el archivo al blob storage.
        // Parámetros de UploadAsync:
        //   - blobName: ruta dentro del contenedor.
        //   - fileStream: stream con el contenido del archivo.
        //   - contentType: MIME type (ej. "application/pdf", "image/png").
        //   - compress: true => comprime con GZip antes de subir (útil para texto/json, no tanto para imágenes/pdf ya comprimidos).
        //   - overwrite: false por defecto => lanza 409 si ya existe. En este caso usamos el default.
        //
        // Alternativa recomendada para operaciones idempotentes o concurrentes:
        // usar blob.UploadOrGetUriAsync en lugar de UploadAsync.
        // UploadOrGetUriAsync devuelve la URI existente si hay conflicto 409,
        // en lugar de lanzar excepcion.
        await blob.UploadAsync(blobName, request.FileStream, request.ContentType, compress: true, cancellationToken: cancellationToken);

        // 8. Simular envío de notificación (con fallos controlados para probar retry/compensación).
        // El parámetro FailedAttempts del comando permite forzar N fallos consecutivos antes del éxito.
        TrySendNotification(created, request.FailedAttempts);

        // 9. Publicar evento de creación (simulado con un mensaje en el bus).
        await messageBus.PublishAsync(new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
                                      new MessagePublishOptions
                                      {
                                          TimeToLive = TimeSpan.FromHours(1),
                                          Headers = new Dictionary<string, string>
                                          {
                                              ["x-correlation-id"] = Activity.Current?.Id ?? "",
                                              ["x-user-id"] = userContext.UserId.ToString(CultureInfo.InvariantCulture),
                                          }
                                      },
                                      cancellationToken);

        // Otra forma de publicar con opciones es usando un "envelope":
        ////await messageBus.PublishAsync(new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
        ////                              new MessagePublishOptions().WithTimeToLive(TimeSpan.FromHours(1))
        ////                                                         .WithHeader("x-correlation-id", Activity.Current?.Id ?? "")
        ////                                                         .WithHeader("x-user-id", userContext.UserId.ToString() ?? "anonymous"),
        ////                              cancellationToken);


        // 10. Si todo ha ido bien, se devuelve el resultado exitoso con los datos del hub creado.
        return new LearningHubResponse(created.Id,
                                       created.Name,
                                       created.Description,
                                       created.Address,
                                       created.Category,
                                       created.Status,
                                       created.CreatedAt,
                                       created.UpdatedAt);
    }

    /// <summary>
    /// Simula el envío de una notificación de bienvenida.
    /// Usa un contador estático para forzar un número concreto de fallos antes de tener éxito,
    /// lo que permite probar el behavior de retry y verificar que las compensaciones se ejecutan correctamente.
    /// </summary>
    private static void TrySendNotification(LearningHubData hub, int failedAttempts)
    {
        var attempt = NotificationAttemptTracker.Next();

        if (attempt <= failedAttempts)
            throw new InvalidOperationException($"Failed to send welcome notification for hub '{hub.Name}' " +
                                                $"(attempt {attempt}/{failedAttempts}).");

        NotificationAttemptTracker.Reset();
    }

    private static Task DeleteHubAsync(int hubId)
    {
        LearningHubStore.Delete(hubId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tracker estático con lock para simular conteo de intentos de notificación entre reintentos.
    /// Solo con fines de demostración del pipeline de retry y compensación.
    /// </summary>
    internal static class NotificationAttemptTracker
    {
        private static int _count;
        private static readonly Lock _lock = new();

        public static int Next()
        {
            lock (_lock)
            { return ++_count; }
        }

        public static void Reset()
        {
            lock (_lock)
            { _count = 0; }
        }
    }

}

/// <summary>
/// Validador de FluentValidation para <see cref="CreateLearningHubCommand"/>.
/// Se registra automáticamente en DI y el <see cref="ValidationBehavior"/> lo ejecuta
/// antes de llegar al handler. Si falla la validación, el pipeline devuelve error 400
/// y ni siquiera se ejecuta el handler ni las compensaciones.
/// </summary>
public sealed class CreateLearningHubCommandValidator : AbstractValidator<CreateLearningHubCommand>
{
    public CreateLearningHubCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FailedAttempts).InclusiveBetween(0, 3);
    }
}
