# PostgreSQL outbox processor
Implement the existing reusable transactional outbox across Akay.To packages and Akay.Be. Working trees: main; no commits authorized.
Delivery: local reviewable units only (no publishing/DDL application); net10.0, existing xUnit runners, disposable PostgreSQL via Docker. Forecast: 1800–2800 authored lines including tests/docs.

## Specs
S1. "No quiero únicamente un diseño o una propuesta. Quiero que analices el código existente, prepares un plan y realices la implementación, incluyendo las pruebas necesarias." "No dupliques funcionalidades existentes ni crees abstracciones innecesarias."
S2. "Implementa la lógica específica de PostgreSQL en `Akay.To.EF.Npgsql`, manteniendo `Akay.To.EF` independiente del proveedor de base de datos." "No introducir dependencias de Rebus en Akay.To.Core o Akay.To.EF."
S3. "Concurrencia: utiliza preferiblemente `FOR UPDATE SKIP LOCKED` junto con `UPDATE ... RETURNING` para reclamar mensajes atómicamente. Realiza la reclamación en una transacción corta y libera los bloqueos antes de publicar en Rebus."
S4. "Leases: implementa identificación del propietario, expiración y protección frente a procesadores que intenten actualizar mensajes después de perder su lease. Utiliza preferiblemente el reloj de PostgreSQL para calcular vencimientos." "Renovación del lease si el procesamiento puede exceder su duración."
S5. "Índices: aprovecha los índices parciales de PostgreSQL para optimizar la búsqueda de mensajes pendientes y reintentos sin indexar innecesariamente los mensajes publicados." "Fechas: utiliza `timestamp with time zone` (`timestamptz`) y valores UTC compatibles con Npgsql para las fechas de procesamiento, publicación y expiración."
S6. "Errores transitorios: contempla deadlocks, conflictos de concurrencia y fallos transitorios de conexión utilizando los mecanismos de Npgsql y las estrategias de reintento existentes, evitando publicaciones duplicadas por reintentos automáticos."
S7. "Utilizar un canal acotado con capacidad para una notificación pendiente." "Permitir múltiples productores y un consumidor por instancia." "Agrupar notificaciones para evitar despertares redundantes." "No transportar mensajes Outbox por el canal, únicamente señales." "Compartir la instancia mediante DI." "Despertar inmediatamente al procesador cuando se detecte trabajo nuevo." "Mantener un polling periódico de respaldo, configurable (por defecto, 60 segundos)." "Procesar los mensajes pendientes al arrancar, sin esperar a la primera notificación."
S8. "No asumas que `SavedChangesAsync` equivale siempre a un commit." "Evita notificaciones innecesarias cuando no se hayan creado registros Outbox." "Si algún escenario no permite detectar el commit de manera fiable, documenta la limitación y garantiza que el polling de respaldo mantiene la corrección." "Bajo ninguna circunstancia debe publicarse un mensaje Outbox antes de que su transacción esté confirmada."
S9. "El Outbox debe implementar semántica **at-least-once**." "Conserva un identificador estable por mensaje Outbox que permita la deduplicación en los consumidores." "El procesamiento de un mensaje no debe eliminar su trazabilidad."
S10. "Número de intentos." "Fecha del siguiente intento." "Backoff exponencial, preferiblemente con jitter." "Número máximo de reintentos configurable." "Registro de errores." "Estado definitivo para mensajes agotados o no recuperables." "Capacidad de recuperar mensajes con leases expirados." "Un mensaje defectuoso no debe bloquear indefinidamente los demás." "Respeta los `CancellationToken` y distingue cancelaciones normales de errores reales."
S11. "El BackgroundService de Akay.Be debe ser extremadamente pequeño." "Toda la lógica de espera, procesamiento por lotes, reintentos y recuperación debe residir en los componentes reutilizables." "No inyectar directamente un `DbContext` scoped en servicios singleton." "Utilizar scopes adecuadamente." "Liberar los recursos después de cada lote." "Gestionar correctamente el cierre de la aplicación." "Capturar y registrar errores recuperables para evitar la terminación permanente del worker." "Permitir desactivar el procesamiento por configuración."
S12. "Centraliza los parámetros relevantes mediante opciones tipadas y validación de configuración." "Polling interval." "Batch size." "Lease duration." "Retry policy." "Maximum attempts." "Enabled/disabled." "Reutiliza el sistema de logging y observabilidad existente."
S13. "Para las garantías de concurrencia y transacciones, utiliza pruebas de integración con el proveedor SQL real. No confíes únicamente en EF Core InMemory o SQLite para verificar comportamientos específicos de Postgre SQL."
S14. "Mantener compatibilidad con las funcionalidades existentes siempre que sea posible." "No crear paquetes NuGet adicionales salvo necesidad real y justificada." "No modificar contratos públicos sin justificarlo." "Preferir clases internas para detalles que no necesiten exponerse públicamente."
S15. "Finalmente: proporciona un resumen de los cambios, las decisiones arquitectónicas, los posibles cambios de esquema/migraciones y las limitaciones conocidas."
S16. "a - sí" — el hosted service del procesador debe vivir en el paquete reutilizable, de modo que una sola llamada en Infrastructure registre procesador, senal, contratos y ejecucion, y el Host quede sin lineas de outbox.
S17. "b - sí" — los eventos que se persisten y se entregan deben declararse con un unico marcador derivado, `IOutboxIntegrationEvent`, en lugar de implementar `IOutboxDomainEvent` e `IIntegrationEvent` por separado.
S18. "c - sí" — una sola seccion de configuracion: los parametros del procesador viven dentro de `DbContextSettings:<Contexto>:Outbox:Processor`, no en una seccion propia.
S19. "d - quita esos tests" — se eliminan `Akay.To.EF.Npgsql.Tests/OutboxPostgreSqlTests.cs` y la suite PostgreSQL asociada. Excepcion consciente a S13: la reclamacion por lease, el fencing, la renovacion, la recuperacion de abandonados, la visibilidad transaccional y los predicados parciales de los indices quedan sin verificacion automatizada contra proveedor real; el DDL revisable y la revision manual son la unica garantia.
S20. "e - sí" — se elimina el test anadido en `Akay.To.Messaging.Rebus` en un paquete sin cambios de produccion.
S21. "1. Añade las validaciones de OutboxProcessorOptions al ApplicationSettingsValidator" — Akay.Be valida los parametros del procesador por contexto al arrancar, junto al resto de la configuracion.
S22. "2. No sería mejor meter el DescribeFailure en el OutboxProcessorOptionsValidator y devolver varios errores si los hubiera?" — el validador de opciones del paquete concentra las reglas y devuelve todas las fallas de una vez; `DescribeFailure` desaparece.
S23. "4. por qué al AddOutboxProcessor le pasas typeof(Akay.Be.Domain.Entities.Academic.AcademicPeriod).Assembly concretamente?" — el ancla del ensamblado de contratos pasa a ser el propio evento entregable (`AcademicPeriodCreatedOutboxEvent`), que es lo que el mapa declara.
S25. User approved moving the processor validation rules to `DbContextOutboxProcessorSettings.Validate` (message: "1. me gusta la idea de mover las reglas de validación a DbContextOutboxProcessorSettings.Validate"), so the rules have one definition consumed by both the package options validator and the application settings validator; and reported that `docs/staging-deployment.md` was restored by the user ("2. el staging-deployment.md ya lo he restaurado yo").
S26. User asked for the reason behind the assembly anchor ("3. explícamelo por qué necesitas el ancla a ensamblados que declaran los mensajes permitidos para entrega").

## Tasks
T1 | S1,S2,S9,S14 | inline shared contracts | dropped | El diseno aprobado en L12 no anade contratos a Akay.To.Core: la entrega reutiliza el IMessageBus existente y el marcador IIntegrationEvent. IOutboxMessagePublisher se descarto; no hubo cambios en Akay.To.Core.
T2 | S2,S7,S8,S10,S11,S12 | inline sequential EF processor | done | Reescrito tras el rollback del 19-oct (L12): OutboxProcessor, OutboxProcessorOptions, OutboxSignal y OutboxServiceCollectionExtensions nuevos; OutboxMessage, DomainEventsSaveChangesInterceptor (senal opcional + fix de cancelacion) y ModelBuilderExtensions modificados. Evidencia: dotnet test Akay.To.EF.sln -c Release -p:UseLocalAkayTo=true: exit0, 35 passed, 0 failed, 0 skipped (L14-L16). Commit not authorized.
T3 | S2,S3,S4,S5,S6,S13 | inline Npgsql provider | done (evidencia invalidada por S19) | Sin cambios de produccion en Akay.To.EF.Npgsql. Tuvo evidencia contra PostgreSQL real (15 passed) que S19 elimino; desde entonces no hay verificacion automatizada de concurrencia, leases ni transacciones: es la excepcion consciente registrada en S19. Commit not authorized.
T8 | S16 | inline EF hosted service | done | Nuevo `OutboxProcessorHostedService<TContext>` interno en Akay.To.EF que ejecuta `RunAsync`; `AddOutboxProcessor` lo registra con `AddHostedService`. Akay.Be deja de tener `src/Akay.Be.Host/Hosting/OutboxBackgroundService.cs` (carpeta eliminada) y su HostRegisterModule ya no registra nada de outbox. Evidencia: Akay.Be Release verde y test `ThePackageRegistersTheHostedServiceSoTheHostNeedsNoWorker`. Commit not authorized.
T9 | S17 | inline Core marker | done | `Akay.To.Core/Application/Abstractions/Outbox/IOutboxIntegrationEvent.cs` (`: IOutboxDomainEvent, IIntegrationEvent`) y los dos eventos de Be migrados a ese unico contrato. Evidencia: Core 189 passed, Akay.Be Release verde. Commit not authorized.
T10 | S18 | inline config unification | done | Core: `DbContextOutboxSettings.Processor` de tipo `DbContextOutboxProcessorSettings` con los siete parametros. Be: la seccion `OutboxProcessor` desaparece de appsettings y sus valores pasan a `DbContextSettings:ApplicationDbContext:Outbox:Processor`. EF: `AddOutboxProcessor<TContext>(settings, assemblies)` resuelve el contexto por `typeof(TContext).Name` y vuelca los valores en `OutboxProcessorOptions`. Evidencia: Core 189, EF 35, Akay.Be Release verde. Commit not authorized.
T11 | S19 | inline test removal | done | Borrado `Akay.To.EF.Npgsql.Tests/OutboxPostgreSqlTests.cs`; la suite Npgsql queda en 5 pruebas de registro/DI, sin exigir PostgreSQL. Docs que citaban esa verificacion actualizadas (OutboxProcessor.md: seccion Verificacion y afirmacion del test de indices). Commit not authorized.
T12 | S20 | inline test removal | done | Revertido `Akay.To.Messaging.Rebus.Tests/RebusMessagingConfigurationTests.cs` a su version en HEAD (git status limpio en ese repositorio); la suite queda en 7 pruebas. Commit not authorized.
T13 | S21 | inline app validation | done | `Akay.Be.Application/ApplicationSettingsValidator` valida por contexto `Outbox.Processor`: PollingInterval, BatchSize, LeaseDuration, RetryInitialDelay, RetryMaxDelay (>= inicial) y MaximumAttempts, con mensajes que nombran la ruta completa. Evidencia: Akay.Be.Application.Tests 109 passed, incluidos dos tests nuevos (configuracion invalida reporta cada propiedad afectada; valores por defecto no generan error de procesador). Commit not authorized.
T14 | S22 | inline package validation | done | Las reglas viven en `OutboxProcessorOptionsValidator`, que acumula todas las fallas y devuelve `ValidateOptionsResult.Fail(list)`; `OutboxProcessorOptions.DescribeFailure` se elimino y la constante `MaximumTimerDelay` paso a `DbContextOutboxProcessorSettings` (limite de temporizador de .NET, compartido por el validador del paquete y el de la aplicacion). Evidencia: Akay.To.EF 36 passed, incluido `ValidatorReportsEveryInvalidValueAtOnce` (cinco valores invalidos -> cinco errores). Commit not authorized.
T15 | S23 | inline anchor | done | `InfrastructureRegisterModule` declara los contratos con `typeof(AcademicPeriodCreatedOutboxEvent).Assembly` y un comentario que explica por que: el ensamblado se usa para descubrir los mensajes entregables, y el ancla es ahora el propio evento. Commit not authorized.
T16 | S24 | inline evidence | done | Ver L19: el borrado de `docs/staging-deployment.md` precede a esta sesion y no fue ejecutado por el agente; el usuario lo restauro el mismo dia (S25). Commit not authorized.
T17 | S25 | inline validation rules in settings | done | `DbContextOutboxProcessorSettings.Validate()` devuelve todos los incumplimientos como `OutboxProcessorSettingError(Property, Message)` y es la unica definicion de las reglas. El validador del paquete (`OutboxProcessorOptionsValidator`) delega en el y reporta todas las fallas; `ApplicationSettingsValidator` las traduce con `Custom` a errores por propiedad, sin reglas duplicadas. Se elimino la sobrecarga `AddOutboxProcessor<TContext>(params Assembly[])`: el unico camino de registro es desde settings, que es de donde salen las reglas. Evidencia: Core 198 passed (9 tests nuevos de reglas), EF 29, Npgsql 5, suite de Akay.Be reejecutada. Commit not authorized.
T18 | S26 | inline documentation | done | `docs/OutboxProcessor.md` explica por que el registro recibe ensamblados: el procesador solo conoce el nombre completo del tipo persistido y necesita el ensamblado del propio evento entregable para resolverlo y deserializarlo. Commit not authorized.
T4 | S2,S9,S14 | inline Rebus unit | done | Sin cambios de produccion en Akay.To.Messaging.Rebus; el test nuevo demuestra que publicar a traves de IIntegrationEvent conserva el tipo concreto del payload. Evidencia: dotnet test Akay.To.Messaging.Rebus.Tests.csproj: exit0, 8 passed, 0 failed, 0 skipped (L14). El .sln del paquete no incluye el proyecto de tests; debe ejecutarse el csproj directamente. Commit not authorized.
T5 | S7,S11,S12,S14 | inline Akay.Be host integration | done | OutboxBackgroundService solo delega en RunAsync, registro AddOutboxProcessor en InfrastructureRegisterModule, opciones tipadas y appsettings (Enabled=false); eventos de Be con IIntegrationEvent. Evidencia: dotnet test Akay.Be.slnx -c Release: exit0, 203 passed (Domain56, Application107, Infrastructure29, Architecture4, Host7), 0 failed, 0 skipped (L14). Commit not authorized.
T6 | S1,S3,S4,S6,S8,S9,S10,S13 | inline seam checks plus independent verification | in_progress | Verificacion independiente gentle-ai-verify lanzada en L13 sobre el codigo del diseno simplificado (read-only, reproduce las cuatro suites y evalua S1-S15); resultado pendiente al escribir esta linea. La verificacion de L9 corresponde a la version anterior y no se reutiliza como evidencia.
T7 | S15 | inline documentation and final evidence reconciliation | done | Docs del diseno simplificado: Akay.To.EF/docs/OutboxProcessor.md, docs/Interceptors.md, docs/outbox-schema-postgresql.sql y akay.be/docs/outbox-processor.md. L14 aclara el nombre real de tabla (DbContextSettings:Outbox:TableName; Akay.Be lo sobrescribe a infra."OutboxMessage") en el DDL, la recuperacion manual y la guia de activacion. Queda la reconciliacion final del resultado de T6.

## Log
L1. Original request (verbatim) follows.

# Implementación del Outbox Processor para Akay.To

## Objetivo

Completar la implementación del patrón Transactional Outbox existente en Akay.To, incorporando el procesamiento y publicación de mensajes pendientes.

La solución debe ser **reutilizable, desacoplada de Rebus, segura en entornos con múltiples instancias y preparada para producción**.

No quiero únicamente un diseño o una propuesta. Quiero que analices el código existente, prepares un plan y realices la implementación, incluyendo las pruebas necesarias.

## Consideraciones específicas de PostgreSQL

La base de datos utilizada actualmente es PostgreSQL mediante `Akay.To.EF.Npgsql`, no SQL Server.

Ten en cuenta los siguientes requisitos:

1. **Concurrencia:** utiliza preferiblemente `FOR UPDATE SKIP LOCKED` junto con `UPDATE ... RETURNING` para reclamar mensajes atómicamente. Realiza la reclamación en una transacción corta y libera los bloqueos antes de publicar en Rebus.

2. **Leases:** implementa identificación del propietario, expiración y protección frente a procesadores que intenten actualizar mensajes después de perder su lease. Utiliza preferiblemente el reloj de PostgreSQL para calcular vencimientos.

3. **Índices:** aprovecha los índices parciales de PostgreSQL para optimizar la búsqueda de mensajes pendientes y reintentos sin indexar innecesariamente los mensajes publicados.

4. **Fechas:** utiliza `timestamp with time zone` (`timestamptz`) y valores UTC compatibles con Npgsql para las fechas de procesamiento, publicación y expiración.

5. **Errores transitorios:** contempla deadlocks, conflictos de concurrencia y fallos transitorios de conexión utilizando los mecanismos de Npgsql y las estrategias de reintento existentes, evitando publicaciones duplicadas por reintentos automáticos.

6. **Transacciones:** verifica el comportamiento real de los interceptores EF Core con Npgsql, incluyendo transacciones explícitas y rollback.

7. **Notificaciones:** mantén el Channel como mecanismo local de activación y el polling como respaldo. No dependas del Channel para coordinar distintas instancias de la aplicación.

Implementa la lógica específica de PostgreSQL en `Akay.To.EF.Npgsql`, manteniendo `Akay.To.EF` independiente del proveedor de base de datos.

## 1. Analizar la implementación existente

Antes de modificar código, inspecciona los proyectos:

- `Akay.To.Core`: abstracciones y contratos compartidos.
- `Akay.To.EF`: interceptor de EF Core que genera registros Outbox, entidades, configuraciones y persistencia.
- `Akay.To.EF.Npgsql`: funcionalidades específicas de Postgre SQL, si corresponde.
- `Akay.To.Messaging.Rebus`: infraestructura de mensajería, publicación y envío de mensajes.
- `Akay.Be`: aplicación que alojará el BackgroundService.

Identifica qué interfaces, servicios, modelos y extensiones de DI existen y cuáles pueden reutilizarse.

**No dupliques funcionalidades existentes ni crees abstracciones innecesarias.**

Respeta las convenciones, arquitectura, patrones, namespaces y estilo de código de Akay.To.

Utiliza las versiones de .NET y de las dependencias existentes en los proyectos.

## 2. Arquitectura y responsabilidades

### Akay.To.Core

Contendrá únicamente las abstracciones compartidas que sean realmente necesarias.

Reutiliza las interfaces existentes de mensajería si permiten desacoplar el procesamiento del proveedor.

Evita introducir dependencias de EF Core, Rebus o cualquier implementación concreta.

### Akay.To.EF

Debe implementar:

- Mecanismo de notificación mediante `Channel<T>`.
- Persistencia, recuperación y reclamación de mensajes Outbox.
- Procesamiento por lotes.
- Gestión de estados y reintentos.
- Coordinación del procesamiento.
- Configuración y registro de dependencias.

La lógica de procesamiento debe estar encapsulada para poder ejecutarse desde cualquier aplicación.

Las operaciones específicas de Postgre SQL deben situarse en `Akay.To.EF.Npgsql`, si encaja con la arquitectura existente.

### Akay.To.Messaging.Rebus

Debe implementar la publicación de mensajes Outbox utilizando la infraestructura de Rebus existente.

Debe soportar los mecanismos de envío/publicación ya utilizados en Akay.To, respetando sus rutas y contratos.

No debe conocer los detalles de persistencia de Outbox.

### Akay.Be

Contendrá un `BackgroundService` con el mínimo código posible.

Preferiblemente, su única responsabilidad será delegar la ejecución en un servicio implementado en Akay.To.EF.

La configuración debe realizarse mediante extensiones de `IServiceCollection` y `IOptions<T>`, siguiendo las convenciones existentes.

## 3. Notificaciones mediante Channel

Implementa un mecanismo de señalización basado en `System.Threading.Channels`.

Requisitos:

- Utilizar un canal acotado con capacidad para una notificación pendiente.
- Permitir múltiples productores y un consumidor por instancia.
- Agrupar notificaciones para evitar despertares redundantes.
- No transportar mensajes Outbox por el canal, únicamente señales.
- Compartir la instancia mediante DI.
- Despertar inmediatamente al procesador cuando se detecte trabajo nuevo.
- Mantener un polling periódico de respaldo, configurable (por defecto, 60 segundos).
- Procesar los mensajes pendientes al arrancar, sin esperar a la primera notificación.

**La base de datos es la fuente de verdad. El Channel únicamente optimiza la latencia.**

El sistema debe seguir funcionando correctamente aunque se pierdan notificaciones, se reinicie la aplicación o los mensajes se creen desde otras instancias.

No debe publicarse un segundo mensaje Rebus para avisar de que existen registros Outbox.

## 4. Integración con SaveChanges y transacciones

Aprovecha el interceptor Outbox existente.

La señal debe emitirse preferiblemente después de confirmar que los registros Outbox han sido persistidos y su transacción ha realizado commit.

Analiza especialmente:

- `SavedChangesAsync` y transacciones implícitas.
- Transacciones explícitas de EF Core.
- `TransactionCommittedAsync`.
- Transacciones ambientales (`TransactionScope`), si están soportadas.
- Rollback y operaciones fallidas.
- Cancelaciones.

No asumas que `SavedChangesAsync` equivale siempre a un commit.

Evita notificaciones innecesarias cuando no se hayan creado registros Outbox.

Si algún escenario no permite detectar el commit de manera fiable, documenta la limitación y garantiza que el polling de respaldo mantiene la corrección.

**Bajo ninguna circunstancia debe publicarse un mensaje Outbox antes de que su transacción esté confirmada.**

## 5. Concurrencia y múltiples instancias

Este requisito es fundamental.

La implementación debe funcionar correctamente con varias instancias de Akay.Be ejecutándose simultáneamente.

No basta con realizar un `SELECT` de mensajes pendientes.

Implementa un mecanismo de reclamación atómica de registros, adecuado al proveedor de base de datos.

Considera:

- Reclamación exclusiva mediante leases.
- Identificador del procesador propietario.
- Fecha de expiración del lease.
- Recuperación de mensajes cuando una instancia muere.
- Protección frente a actualizaciones realizadas por propietarios cuyo lease haya expirado.
- Renovación del lease si el procesamiento puede exceder su duración.
- Procesamiento por lotes configurable.

Evita mantener bloqueos o transacciones de base de datos abiertos mientras se realizan llamadas externas a Rebus.

El sistema debe permitir procesamiento paralelo entre instancias, sin que dos procesadores reclamen simultáneamente el mismo registro válido.

## 6. Publicación, duplicados e idempotencia

El Outbox debe implementar semántica **at-least-once**.

Contempla expresamente el escenario:

1. El mensaje se publica correctamente en Rebus.
2. La aplicación falla antes de marcarlo como publicado.
3. Otro procesador vuelve a publicar el mensaje.

No intentes prometer garantías exactly-once imposibles entre SQL y un broker independiente.

Conserva un identificador estable por mensaje Outbox que permita la deduplicación en los consumidores.

Analiza la infraestructura de mensajería existente y reutiliza cualquier mecanismo de idempotencia o deduplicación disponible.

El procesamiento de un mensaje no debe eliminar su trazabilidad.

## 7. Gestión de errores y reintentos

Implementa:

- Número de intentos.
- Fecha del siguiente intento.
- Backoff exponencial, preferiblemente con jitter.
- Número máximo de reintentos configurable.
- Registro de errores.
- Estado definitivo para mensajes agotados o no recuperables.
- Capacidad de recuperar mensajes con leases expirados.

Un mensaje defectuoso no debe bloquear indefinidamente los demás.

Respeta los `CancellationToken` y distingue cancelaciones normales de errores reales.

Ten en cuenta que el polling debe procesar los reintentos cuando corresponda, sin depender de nuevas notificaciones.

## 8. BackgroundService y DI

El BackgroundService de Akay.Be debe ser extremadamente pequeño.

Toda la lógica de espera, procesamiento por lotes, reintentos y recuperación debe residir en los componentes reutilizables.

Requisitos adicionales:

- No inyectar directamente un `DbContext` scoped en servicios singleton.
- Utilizar scopes adecuadamente.
- Liberar los recursos después de cada lote.
- Gestionar correctamente el cierre de la aplicación.
- Capturar y registrar errores recuperables para evitar la terminación permanente del worker.
- Permitir desactivar el procesamiento por configuración.
- Mantener la posibilidad de alojar el procesador en un Worker Service independiente en el futuro.

Evita crear un HostedService por cada tipo de mensaje.

## 9. Configuración y observabilidad

Centraliza los parámetros relevantes mediante opciones tipadas y validación de configuración.

Incluye, como mínimo:

- Polling interval.
- Batch size.
- Lease duration.
- Retry policy.
- Maximum attempts.
- Enabled/disabled.

Reutiliza el sistema de logging y observabilidad existente.

Registra información útil sobre mensajes procesados, fallidos, reintentados, duración del procesamiento y mensajes pendientes, sin introducir dependencias innecesarias.

## 10. Pruebas

Añade pruebas automatizadas para los escenarios críticos:

- Creación de un registro Outbox.
- Despertar del procesador mediante Channel.
- Agrupación de múltiples notificaciones.
- Procesamiento inicial y polling de respaldo.
- Publicación correcta y actualización del estado.
- Error de publicación y reintento.
- Agotamiento de reintentos.
- Procesamiento concurrente desde múltiples instancias.
- Expiración y recuperación de leases.
- Fallo posterior a la publicación y posible duplicado.
- Commit y rollback de transacciones.
- Cancelación y cierre del BackgroundService.

Para las garantías de concurrencia y transacciones, utiliza pruebas de integración con el proveedor SQL real. No confíes únicamente en EF Core InMemory o SQLite para verificar comportamientos específicos de Postgre SQL.

## 11. Restricciones arquitectónicas

- No introducir dependencias de Rebus en Akay.To.Core o Akay.To.EF.
- No crear paquetes NuGet adicionales salvo necesidad real y justificada.
- No duplicar interfaces o servicios existentes.
- No modificar contratos públicos sin justificarlo.
- Mantener compatibilidad con las funcionalidades existentes siempre que sea posible.
- Respetar los principios SOLID sin sobreingeniería.
- Preferir clases internas para detalles que no necesiten exponerse públicamente.
- Mantener separadas las responsabilidades de persistencia, procesamiento y publicación.

## 12. Forma de trabajo

**Primero:** inspecciona todos los componentes existentes y determina qué puede aprovecharse.

**Segundo:** presenta un breve resumen de la arquitectura encontrada, las decisiones técnicas y las modificaciones necesarias.

**Tercero:** implementa la solución completa, adaptándola al código real, no a clases o interfaces hipotéticas.

**Cuarto:** añade y ejecuta las pruebas correspondientes, compila los proyectos afectados y corrige los problemas encontrados.

**Finalmente:** proporciona un resumen de los cambios, las decisiones arquitectónicas, los posibles cambios de esquema/migraciones y las limitaciones conocidas.

Presta especial atención a la fiabilidad ante concurrencia, fallos de proceso y transacciones.

No dejes estos aspectos como mejoras futuras: **forman parte de los requisitos de esta implementación**.

L2. User authorization: "Autorizar Akay.To y Akay.Be" — reads, changes, tests and delegation authorized; preserve existing local edits; commits, publishing and applying DDL not authorized.
L3. User test environment selection: "Docker Desktop". Docker server 29.4.3 verified after user started it. Only a disposable test database will be provisioned.
L4. Discovery: Akay.To is a container of independent package Git repositories, not a repository root. Core/EF/Npgsql/Rebus were initially clean. Akay.Be has pre-existing staged deletions and modified project/config files; preserve them.
L5. Design seam: existing IMessageBus cannot publish stored arbitrary IOutboxDomainEvent or set reserved stable Rebus message IDs. Add only IOutboxMessagePublisher to Core, taking existing IOutboxMessage. Persisted-lease gateway belongs in EF, implemented by Npgsql. No existing contracts removed.
L6. Tooling blocker: all three independent repository writer launches rejected before queueing with exact message `repository_root must select a canonical independent Git repository.` Git rev-parse independently confirms the exact supplied EF/Npgsql/Rebus roots and independent .git directories. No writer ran; no native Agent fallback is exposed. Do not relaunch equivalent failed work or bypass required routing. Runtime root validation needs correction before delegation can resume.
L20. Requirement change (verbatim user message): "1. me gusta la idea de mover las reglas de validación a DbContextOutboxProcessorSettings.Validate / 2. el staging-deployment.md ya lo he restaurado yo / 3. explícamelo por qué necesitas el ancla a ensamblados que declaran los mensajes permitidos para entrega". Implemented as S25-S26 and T17-T18. (1) The rules now live only in `DbContextOutboxProcessorSettings.Validate()` (Core), returning `OutboxProcessorSettingError(Property, Message)` items so each consumer keeps the property name: the package's `OutboxProcessorOptionsValidator` closes over the resolved settings and maps them to `ValidateOptionsResult.Fail(list)` (all errors at once), and Akay.Be's `ApplicationSettingsValidator` translates them per property through a `Custom` rule. The previously duplicated numeric ranges are gone. Consequence recorded: the parameterless `AddOutboxProcessor<TContext>(params Assembly[])` overload was removed, because with the rules in the settings there is no way to validate an externally configured options object; registration has exactly one path (from `BaseApplicationSettings`), and the EF and Akay.Be tests now build settings for it. Rule coverage moved from `Akay.To.EF.Tests` to `Akay.To.Core.Tests` (nine tests: defaults valid, each value out of range, every invalid value at once, retry bounds, timer limit, and the boundary that accepts the maximum). (2) The user restored `docs/staging-deployment.md`; the file is no longer deleted in the working tree and no action is pending from the agent. (3) The assembly anchor is documented in `docs/OutboxProcessor.md` and explained in the reply. Evidence: Core 198 passed / 0 failed / 0 skipped (nine new rule tests); Akay.To.EF 29 / 0 / 0; Akay.To.EF.Npgsql 5 / 0 / 0; Akay.Be Release 206 / 0 / 0 (Domain 56, Application 109, Infrastructure 29, Architecture 4, Host 8). One implementation detail worth recording: FluentValidation does not prefix the property name passed to `AddFailure`, and `ValidationContext.PropertyName` is obsolete, so the adapter builds the full path with `context.PropertyPath` plus the failing property. No commits, no package publishing, no DDL applied.

L19. Requirement change (verbatim user message): "1. Añade las validaciones de OutboxProcessorOptions al ApplicationSettingsValidator / 2. No sería mejor meter el DescribeFailure en el OutboxProcessorOptionsValidator y devolver varios errores si los hubiera? / 3. por qué has borrado el staging-deployment.md? Si no es tuyo / 4. por qué al AddOutboxProcessor le pasas typeof(Akay.Be.Domain.Entities.Academic.AcademicPeriod).Assembly concretamente?". Implemented as S21-S24 and T13-T16. (1) Akay.Be's ApplicationSettingsValidator now validates the per-context `Outbox.Processor` values, mirroring the package rules and messaging style used by the base validator; the shared timer bound moved to `DbContextOutboxProcessorSettings.MaximumTimerDelay` so both validators name the same limit. (2) `DescribeFailure` is gone: the rules live in `OutboxProcessorOptionsValidator`, which collects every failure and returns `ValidateOptionsResult.Fail(list)`; the EF tests were rewritten against the validator and a new `ValidatorReportsEveryInvalidValueAtOnce` pins the multi-error behaviour. (4) The contract-assembly anchor is now `AcademicPeriodCreatedOutboxEvent`, the deliverable itself, instead of `AcademicPeriod`. (3) Evidence about `docs/staging-deployment.md`: it is NOT deleted by this agent. Proof: (a) the very first `git status --short` of this session, taken before any edit, already listed ` D docs/staging-deployment.md`; (b) Engram observation id 346 (2026-10-09, AI fast-transcription session) records "Existing Be docs/staging-deployment.md deletion was untouched", so it already existed nine days earlier; (c) the file exists in HEAD (`git cat-file -e HEAD:docs/staging-deployment.md` succeeds) and the deletion is unstaged in the working tree; (d) the deletion commands this agent ran targeted only `.outbox-backup-20261019-000834/` directories, `src/Akay.Be.Host/Hosting/` and `Akay.To.EF.Npgsql.Tests/OutboxPostgreSqlTests.cs`; (e) `README.md:7` still links to that document, which is consistent with an unresolved deletion rather than a curated removal. Restoring it is one command (`git checkout -- docs/staging-deployment.md`) and remains the user's decision. Evidence after this change: Core 189, Akay.To.EF 36, Akay.Be.Application 109, Akay.Be Release suite re-run. No commits, no package publishing, no DDL applied.

L18. Requirement change (verbatim user answers, responding to the five numbered options offered): "a - sí", "b - sí", "c - sí", "d - quita esos tests", "e - sí". Implemented as S16-S20 and T8-T12. (a) The reusable package now owns execution: `OutboxProcessorHostedService<TContext>` (internal, Akay.To.EF) is registered by `AddOutboxProcessor`, Akay.Be deleted `src/Akay.Be.Host/Hosting/` and its host module no longer mentions the outbox. (b) `IOutboxIntegrationEvent` added to Akay.To.Core as the single marker (`: IOutboxDomainEvent, IIntegrationEvent`), both Akay.Be events migrated; the base interface was deliberately left untouched. (c) Configuration unified: Core's `DbContextOutboxSettings` gained a `Processor` object (`DbContextOutboxProcessorSettings`, seven values) and the standalone `OutboxProcessor` appsettings section was removed in favour of `DbContextSettings:ApplicationDbContext:Outbox:Processor`; `AddOutboxProcessor<TContext>(settings, assemblies)` resolves the context by `typeof(TContext).Name` like `AddEFContext`, so the Host binds nothing. Akay.To.EF now declares `Microsoft.Extensions.Hosting.Abstractions` explicitly (already present transitively) because its public API registers a hosted service. (d) `Akay.To.EF.Npgsql.Tests/OutboxPostgreSqlTests.cs` deleted, accepted as a conscious exception to S13: no automated real-provider verification of claims, leases, transactions or the partial-index predicates remains. (e) The Rebus test added without production changes was reverted to HEAD to keep a no-change package untouched. Evidence after the change: Core 189 passed / 0 failed / 0 skipped; Akay.To.EF 35 / 0 / 0; Akay.To.EF.Npgsql 5 / 0 / 0 (no PostgreSQL needed any more); Akay.To.Messaging.Rebus 7 / 0 / 0; Akay.Be Release 204 / 0 / 0 (Domain 56, Application 107, Infrastructure 29, Architecture 4, Host 8). One real defect was found by the new host test and fixed: `AddOutboxProcessor` registered a hosted service whose graph needs `TimeProvider` (and `ILogger<T>`) but the package did not guarantee the first one, so the registration was only resolvable when another module happened to register it; `AddOutboxProcessor` now does `TryAddSingleton(TimeProvider.System)` exactly like `AddEFContext` (ServiceCollectionExtensions.cs:206). Logging stays a host responsibility by repo convention (Core's `ObservabilityConfiguration.AddLogging()`), so the host-registration test supplies it as a real host would; Akay.Be.Host.Tests gained `ThePackageRegistersTheHostedServiceSoTheHostNeedsNoWorker` (Host 7 -> 8). No commits, no package publishing, no DDL applied.

L17. Workspace cleanup requested by the user after the work was closed: the five `backup/outbox-20261019-000834` branches were deleted (all five pointed at commits already contained in `main`, 0 unique commits, none checked out in a worktree, so nothing was lost), and the six `.outbox-backup-20261019-000834/` directories were removed (akay.be, Akay.To.Core, Akay.To.EF, Akay.To.EF.Npgsql, Akay.To.Messaging.Rebus and Akay.To/knowledge). L12's reference to those directories is history, not current state: the reverted oversized implementation no longer has a snapshot on disk. The user explicitly forbade committing: no commit, no push and no tag was created in any repository; HEADs remain 2135366 (akay.be), 7ba8c50 (Core), 43e526b (EF), cf68840 (EF.Npgsql) and 605f1d0 (Messaging.Rebus), and all work stays uncommitted for the user's decision. The disposable PostgreSQL container akay-outbox-tests-202610 was stopped.

L16. Bounded re-verification of the L15 correction round returned NO remaining blockers and closed B1. Verdicts: B1 PASS/closed — both filters verified byte-identical between the model and the SQL script, and live EXPLAIN (ANALYZE) on the disposable PostgreSQL with 20 000 published + 200 pending rows shows no Seq Scan: pending selection uses an Index Scan with Index Cond "ProcessedAt" IS NULL reading 189 of 5 200 rows, and expired-lease recovery uses an Index Scan on IX_OutboxMessages_LeaseExpiresAt with Index Cond "LeaseExpiresAt" IS NOT NULL AND "LeaseExpiresAt" <= now(). A1 PASS, A2 PASS, A5 PASS. Suites reprinted by the verifier: EF 33/0/0 (at that moment) and PostgreSQL 15/0/0, both exit 0.
Two weaknesses the verifier found in my new tests were corrected: (1) the index test asserted only the presence of WHERE, so a wrong predicate would still pass — it now asserts the exact predicates of both indexes, including that the pending index does NOT carry LeaseExpiresAt, and that the lease index requires "LeaseExpiresAt" IS NOT NULL; (2) the retry-bound validator test asserted Contains("RetryInitialDelay"), which the RetryMaxDelay message also satisfies — it is now two facts with exact expected messages plus a boundary test that MaximumTimerDelay itself is accepted and RetryDelay at attempt 500 stays within the cap. A stale document found as a new advisory was also fixed: docs/ModelBuilderExtensions.md showed the pre-change OutboxMessage model, and now lists the five delivery columns and both partial indexes. Wording correction recorded: L15 called A5 an "overflow" risk, but the pre-fix unreachable literal overflow was already impossible (NextDouble() < 1.0); the real failure mode was now + RetryDelay / Task.Delay out of range, which the bound removes. Suites after the test strengthening: Akay.To.EF 35 passed / 0 failed / 0 skipped (exit0) and Akay.To.EF.Npgsql real PostgreSQL 15 passed / 0 failed / 0 skipped (exit0). Rebus 8 and Akay.Be 203 remain as evidenced in L15 — no production code changed after that run, only test assertions and documentation. New advisory from this round: the planner prefers the pre-existing unfiltered IX_OutboxMessages_ProcessedAt for the pending selection shape (ORDER BY OccurredAt with the OR on NextAttemptAt), so the new partial pending index is created and correct but not currently chosen for that query; documented rather than forced, since the partial index becomes attractive as published rows dominate. No commits, no package publishing, no DDL applied.

L15. Correction round driven by the independent verification's blocker B1 (S5 partial indexes) and selected advisories. Evidence first: two new PostgreSQL tests were written and observed RED before any fix — `OutboxIndexesUsedForPendingMessagesArePartial` failed with "Assert.Contains() Failure: Sub-string not found" for both indexes, and `AbandonedFinalClaimKeepsTheStoredErrorMessage` failed with "Assert.Equal() Failure: Strings differ" because the generic text replaced the stored error. Fixes: B1 — `HasFilter` added to both new indexes in ModelBuilderExtensions (
    "ProcessedAt" IS NULL AND "FailedAt" IS NULL for IX_OutboxMessages_Pending; plus "LeaseExpiresAt" IS NOT NULL for IX_OutboxMessages_LeaseExpiresAt) and the same WHERE clauses written into docs/outbox-schema-postgresql.sql, so published rows are no longer indexed. A1 — the abandoned-final-claim recovery now preserves an existing stored error (message.Error ?? ExhaustedError) instead of overwriting it, keeping traceability while still setting FailedAt. A2 — each processing cycle now logs claimed/delivered/failed counts and elapsed milliseconds; a cycle with nothing claimed logs at Debug and a full batch logs a Debug note, so the Information stream stays useful. A5 — DescribeFailure now rejects RetryInitialDelay and RetryMaxDelay above the timer limit, which removes the (long)(delay*jitter) overflow risk; OutboxProcessorTests gained ValidatorRejectsRetryBoundsBeyondTheTimerLimit. Suites after the fixes, all green: Akay.To.EF 33 passed / 0 failed / 0 skipped; Akay.To.EF.Npgsql (real PostgreSQL) 15 passed / 0 failed / 0 skipped; Akay.To.Messaging.Rebus 8 passed / 0 failed / 0 skipped; Akay.Be Release 203 passed (Domain56, Application107, Infrastructure29, Architecture4, Host7) / 0 failed / 0 skipped. Advisories left as documented decisions rather than silent gaps: A3 app-clock leases, A4 public OutboxSignal for cross-assembly DI, A6 no retention/purge policy (rows are never deleted), A7 pre-existing SQL-Server-style filter syntax on IX_ChangeAudit_AggregateSyncId_OccurredAt (out of scope), A8 the drain loop that repeats while a batch is full. A bounded re-verification of exactly this delta was launched (L15 continuation) and its verdict is recorded when it returns. No commits, no package publishing, no DDL applied.

L14. Resumed execution of L13, with evidence and two defects corrected. Suites (all after the fixes, real exit codes): Akay.To.EF 32 passed / 0 failed / 0 skipped; Akay.To.EF.Npgsql against real PostgreSQL 13 passed / 0 failed / 0 skipped; Akay.To.Messaging.Rebus 8 passed / 0 failed / 0 skipped; Akay.Be Release 203 passed (Domain56, Application107, Infrastructure29, Architecture4, Host7) / 0 failed / 0 skipped.
Defect 1 (test harness, not production): the PostgreSQL suite failed 1/13 with "The message was not marked as failed after exhausting its attempts". Direct instrumentation of the processor and of the test bus showed the bus threw on the first attempt, yet the row was marked processed. Root cause: in OutboxPostgreSqlTests.RecordingBus, the overload `public async Task PublishAsync<TEvent>(TEvent message, MessagePublishOptions options, CancellationToken) => RecordAsync(...);` is an expression-bodied async method WITHOUT await, so it fired the inner task (CS4014) and returned a successful task; the processor therefore saw a successful publication. Verified independently with a scratch project: an expression-bodied `async Task` method without await reports RanToCompletion while the inner task faults unseen. Fixed by removing `async` from that delegation so it returns the inner task. Production is NOT affected: RebusMessageBus.PublishAsync is a block-bodied async method with awaits. The same pattern was checked in the new production files and none remains.
Defect 2 (test compile): Akay.To.Messaging.Rebus.Tests/RebusMessagingConfigurationTests.cs used IHostedService without `using Microsoft.Extensions.Hosting;`, so the repository's test project did not compile and `dotnet test` on its .sln silently ran nothing (the .sln does not include the test project). Fixed the using and ran the test project directly.
Documentation correction: DbContextSettings:Outbox:TableName defaults to __OutboxMessages but Akay.Be overrides it to OutboxMessage, so the real table is infra."OutboxMessage". The DDL script, the manual-recovery statement and the Akay.Be activation guide now name the setting explicitly instead of assuming the default name.
Temporary diagnostic instrumentation (console logging in the processor, logger provider and state prints in the PostgreSQL test) was fully reverted; no Console output remains in the reviewed files. No commits, no package publishing, no DDL applied.

L12. User approval of the simplified design (verbatim, message truncated by the harness): "Apruebo el diseno simplificado, incluyendo la renovacion de leases. Unicamente comprueba que anadir IIntegrationEvent a los eventos actuales sea semanticamente correcto, no una adaptacion artificial [...]". Parent verification answered that question: both Akay.Be outbox records are business facts already serialized by the outbox, so implementing IIntegrationEvent additively (keeping IOutboxDomainEvent, no global inheritance change) is semantically correct and not an artificial adapter; it is what lets the existing IMessageBus/ RebusMessageBus deliver the stored payload without new Core contracts. Consequences of the approval, recorded here: IOutboxMessagePublisher is dropped (T1 dropped); the Channel signal returns as latency-only optimisation with polling as fallback; lease renewal is kept; provider-specific production code in Akay.To.EF.Npgsql and Akay.To.Messaging.Rebus is not needed. Backup taken before the rollback at 2026-10-19 00:08:34 in every affected repository (.outbox-backup-20261019-000834/ with head.txt, index.patch, worktree.patch, untracked.tar, status.txt). The oversized implementation was reverted and the simplified one written between 00:18 and 00:21; the session then ended abruptly.

L13. User instruction to resume (verbatim): "retoma desde ahi y reviso todo luego". Scope: finish the pending checks, docs and feature-document reconciliation of the simplified design; the user reviews the result afterwards. No commits, no package publishing, no DDL application.

L11. User stop and design decisions (verbatim answers): interdependencies question answered by the parent, then the user chose (a) "Sin interceptores nuevos: solo polling" for the commit signal and (b) "Dejarlo detenido, sin tocar nada" for the working tree. Consequences accepted explicitly: no interceptor observes saves or transactions, so the only activation is host startup plus PollingInterval; the immediate-wake part of S7 is consciously waived, with up to the polling interval (default 60 s) of added latency and no correctness loss because claims only read committed rows. Interpretation of "solo polling" recorded for resumption: the signal machinery has no remaining producer, so OutboxSignal, OutboxNotificationState, OutboxSaveChangesInterceptor and OutboxTransactionInterceptor all become removable, together with their DI registrations and the signal-based wait in OutboxProcessor; the cancellation-state fix in DomainEventsSaveChangesInterceptor is independent and should be kept. Nothing was modified after this decision: no file edited, no process started, no commit, no DDL.

L10. Process incident during advisory closure: the chained run job-29 reported exit 0 while the EF project had actually failed to compile, because each step ended in a pipe (tail) and that pipeline's status masked the dotnet failure. Root cause identified by reading the log, not the exit code. The underlying defect was mine: the new RetryDelay arrow body had redundant parentheses, rejected as error IDE0047 at OutboxProcessor.cs:113. Corollary lesson applied: every following multi-step command reports the real dotnet status explicitly per step (PIPESTATUS) instead of trusting a single chained exit code. Fix applied; EF and PostgreSQL suites had not executed in job-29, so no success was claimed for them; rerun job-30 in flight.

L9. Independent verification (task T6) completed by gentle-ai-verify reading the specification and full file contents (new files are untracked, so no diff-based review was possible). It reproduced exactly: EF 33 passed, PostgreSQL 22 passed, Rebus 15 passed, Akay.Be Release 203 passed (Domain56, Application107, Infrastructure29, Architecture4, Host7), all with 0 failed and 0 skipped, and repo status unchanged afterwards. Verdicts: S1-S15 met; no blocking findings; no correction round required. Advisories: (a) exponential backoff growth beyond the first attempt and the RetryMaxDelay cap were unasserted because the EF fake store always yielded Attempts=0; (b) upgrade-outbox.sql had no test tying it to the EF model; (c) a hung broker send can hold a batch because the heartbeat renews indefinitely -- documented as an operator transport-timeout requirement; (d) OutboxSignal is public for cross-assembly DI/test use. Parent closed advisories (a) and (b) as own coverage completion rather than a verifier-triggered correction: RetryDelay gained an internal pure ComputeRetryDelay(initial,max,attempts,jitter) unit-tested for 1s/2s/4s/cap, 50% jitter, saturation at int.MaxValue and invalid jitter; a new model-only OutboxSqlTemplateTests asserts every ADD COLUMN and both partial-index filters come from EF metadata values rather than free text. Advisories (c) and (d) remain documented design decisions.

L8. Continuation correction: independent repository writer selection is unavailable, but inline sequential implementation remains authorized. The parallelism routing reason cannot be delivered and therefore does not fire. Independent verify will use the existing session root and read authorized sibling paths, without repository_root. Do not repeat rejected writer launches. User asked: "ok y entonces que necesitas para continuar"; no additional authorization needed.
L7. Plan audit requested by user: T1 reverted from done to pending because declarations have not been compiled; T2 blocked on external harness availability; T3-T7 remain pending and are not claimed implemented. No RED/GREEN/build/integration-test evidence exists. PostgreSQL provisioning succeeded (Docker server29.4.3, postgres:17 pg_isready exit0), which is environment readiness, not behavioral verification. Owned disposable container will be stopped on handoff and auto-removed (--rm).
