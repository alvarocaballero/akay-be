# Backend notifications
Branch: `feat/notifications`; baseline: `42eb7d498987368797158ba9f9ea872f9813698d`; runner: `dotnet test Akay.Be.slnx --configuration Release`.
Delivery: local implementation only, no commits or publishing authorized; forecast approximately 1,500 authored lines. Route: inline, no independent writer units.

## Specs

- S1: "El sistema pertenecerá a Akay.Be y utilizará los building blocks existentes de Akay.To." "No crear un nuevo proyecto `Akay.Be.Modules.Notifications`." Domain Notification is explicitly authorized by L2; contracts/orchestration in Application, EF in Infrastructure, REST/hub mapping in Host.
- S2: "El servicio determinará si debe persistir la notificación comprobando si implementa dicha interfaz." Preserve the supplied generic SendAsync contract and interfaces. Test ephemeral sends without persistence and persistent commit before publish.
- S3: "Un error de persistencia impedirá publicar la notificación." "El error de SignalR no provocará un reintento de persistencia ni anulará el registro almacenado." Log publication failures; no Outbox. Business persistence is independent.
- S4: "Esquema: `notifications`" "Tabla: `notifications`". Guid Id; text UserId/Type/Title/Message; nullable Url; nullable jsonb Payload; UTC timestamptz CreatedAt/ReadAt. User/date index and unread partial index. No tenant/soft-delete/migrations. Test PostgreSQL with model-created schema and provide DDL for review only.
- S5: "Toda consulta y modificación de notificaciones se filtrará mediante el identificador del usuario autenticado." "Una notificación inexistente o no perteneciente al usuario no deberá revelar información de otros usuarios." Prove authentication, ownership and indistinguishable not-found results.
- S6: "El listado admitirá paginación y filtro opcional por estado de lectura." "Las operaciones de marcado como leído serán idempotentes." "DELETE realizará eliminación física." Implement all five exact /notifications routes from L1 using dispatcher and existing Result/response conventions; order CreatedAt descending.
- S7: "Los contratos enviados mediante SignalR deberán ser DTOs independientes de las entidades EF Core." "El sistema admitirá múltiples conexiones simultáneas del mismo usuario." Use Akay.To publisher, matching local user identity strings, Type/Payload and persistent metadata. Verify multiple connections locally.
- S8: All exclusions in L1 remain binding. No frontend, concrete business events, email/SMS/push, preferences, guaranteed delivery, deduplication, correlation, device read sync or retention.

## Tasks

- T1 [S1-S4,S7-S8] inline, completed; Domain model, Application contracts/service and EF repository. RED: new service/repository tests failed compilation on missing types; GREEN: 7 service tests and 6 real PostgreSQL tests passed. Risk: items 1, 2, 3. Commit: not authorized.
- T2 [S5-S7] inline, completed; dispatcher slices, protected REST, receive-only hub. RED: handler tests failed on missing types; GREEN: 15 Application notification tests and 11 Host tests passed, including real JWTs and three WebSockets. Risk: items 1, 2. Commit: not authorized.
- T3 [S1-S8] inline implementation, in progress; DDL/docs and functional verification completed; independent verifier met S1-S8 with no blockers; native review consent/outcome pending. Final suite: 260 passed, zero failed/skipped. Risk also includes item 5: CI explicitly preloads the disposable PostgreSQL image. Commit: not authorized.

## Log

### L1 — Original request, verbatim

# SPEC: Notification System for Akay.Be

**Status:** Draft  
**Target:** Akay.Be  
**Stack:** .NET 10, EF Core 10, PostgreSQL, ASP.NET Core SignalR, Azure SignalR Service  
**Architecture:** Modular Monolith, Vertical Slice

## 1. Objetivo

Implementar un sistema de notificaciones dirigidas a usuarios concretos, con soporte para publicación en tiempo real y persistencia opcional.

Se distinguirán dos tipos:

- **Ephemeral notifications:** enviadas exclusivamente mediante SignalR.
- **Persistent notifications:** almacenadas en PostgreSQL y publicadas mediante SignalR.

El sistema pertenecerá a Akay.Be y utilizará los building blocks existentes de Akay.To.

No se implementará Outbox ni un mecanismo de entrega garantizada.

## 2. Organización de la solución

Se mantendrán los proyectos existentes.

| Proyecto | Responsabilidad |
|---|---|
| Akay.Be.Application | Contratos y orquestación |
| Akay.Be.Infrastructure | Persistencia PostgreSQL |
| Akay.Be.Host | API REST y configuración SignalR |
| Akay.Be.Domain | Sin cambios inicialmente |

El código de notificaciones se agrupará en carpetas `Notifications` dentro de cada proyecto.

No crear un nuevo proyecto `Akay.Be.Modules.Notifications`.

La integración genérica con SignalR corresponderá a Akay.To.Azure.

## 3. Contratos

```csharp
public interface INotification
{
    string UserId { get; }
    string Type { get; }
}

public interface IPersistentNotification : INotification
{
    string Title { get; }
    string Message { get; }
    string? Url { get; }
}
```

`IPersistentNotification` será una interfaz marcadora extendida con los datos necesarios para su almacenamiento.

El servicio determinará si debe persistir la notificación comprobando si implementa dicha interfaz.

```csharp
public interface INotificationService
{
    Task SendAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
```

Los contratos pertenecerán a Application y podrán ser utilizados por las demás funcionalidades de Akay.Be.

No se acoplarán a los eventos de dominio ni a los mensajes de integración.

## 4. NotificationService

### 4.1. Notificaciones efímeras

1. Recibir la notificación.
2. Publicarla mediante SignalR al usuario destinatario.
3. No persistir ningún registro.

### 4.2. Notificaciones persistentes

1. Recibir la notificación.
2. Generar su identificador único.
3. Persistirla en PostgreSQL.
4. Confirmar la persistencia.
5. Publicarla mediante SignalR incluyendo el identificador generado.

### 4.3. Gestión de errores

No utilizar Outbox.

- Un error de persistencia impedirá publicar la notificación.
- Un error de SignalR después de persistir se registrará mediante el sistema de logging existente.
- El error de SignalR no provocará un reintento de persistencia ni anulará el registro almacenado.
- La notificación persistente seguirá disponible para posteriores consultas REST.
- Las notificaciones efímeras podrán perderse cuando no haya conexión.

Se acepta expresamente un comportamiento *best effort*.

Las notificaciones no garantizan entrega ni lectura.

Los procesos de negocio deberán confirmar sus operaciones independientemente del éxito de publicación en SignalR.

## 5. Persistencia PostgreSQL

Se utilizará EF Core con Npgsql y el DbContext existente, siguiendo las convenciones de Akay.To.EF.

Esquema: `notifications`  
Tabla: `notifications`

### 5.1. Entidad

| Propiedad | Tipo .NET | PostgreSQL |
|---|---|---|
| Id | Guid | uuid |
| UserId | string | text |
| Type | string | text |
| Title | string | text |
| Message | string | text |
| Url | string? | text |
| Payload | string? | jsonb |
| CreatedAt | DateTimeOffset | timestamptz |
| ReadAt | DateTimeOffset? | timestamptz |

### 5.2. Payload

Se almacenará como JSONB en PostgreSQL.

La propiedad .NET podrá mantenerse como `string?`, configurada mediante:

```csharp
builder.Property(x => x.Payload)
    .HasColumnType("jsonb");
```

El servicio será responsable de serializar a JSON válido los datos adicionales de la notificación.

No se realizarán inicialmente consultas sobre propiedades internas del JSON.

### 5.3. Identificación de usuarios

No incluir `TenantId`.

Los `UserId` de Akay.Be son globalmente únicos, incluso entre diferentes tenants.

Toda consulta y modificación de notificaciones se filtrará mediante el identificador del usuario autenticado.

### 5.4. Propiedades opcionales

**Url**

Ruta de navegación interna de la aplicación.

Puede ser nula cuando la notificación no tenga una pantalla de destino.

**Payload**

JSON opcional con datos específicos del mensaje.

No se incluirán `CorrelationId` ni `DeduplicationKey` como columnas.

### 5.5. Índices

Crear índices adecuados para:

- Consultar las notificaciones de un usuario ordenadas por `CreatedAt` descendente.
- Consultar y contar notificaciones no leídas (`ReadAt IS NULL`).

No crear índices adicionales sobre el JSONB sin requisitos concretos de consulta.

### 5.6. Eliminación y retención

Se permitirá eliminar físicamente una notificación.

No utilizar soft delete.

La limpieza periódica de registros antiguos podrá incorporarse posteriormente mediante una política de retención configurable.

## 6. API REST

Implementar los endpoints necesarios siguiendo las convenciones de Akay.Be y su implementación propia del patrón Mediator.

| Método | Endpoint | Descripción |
|---|---|---|
| GET | `/notifications` | Listado paginado |
| GET | `/notifications/unread-count` | Número de no leídas |
| PATCH | `/notifications/{id}/read` | Marcar como leída |
| PATCH | `/notifications/read-all` | Marcar todas como leídas |
| DELETE | `/notifications/{id}` | Eliminar notificación |

### Requisitos

- Los endpoints requerirán autenticación.
- El `UserId` se obtendrá del contexto autenticado.
- Nunca se permitirá consultar, modificar o eliminar notificaciones de otros usuarios.
- El listado admitirá paginación y filtro opcional por estado de lectura.
- Las operaciones de marcado como leído serán idempotentes.
- DELETE realizará eliminación física.
- Una notificación inexistente o no perteneciente al usuario no deberá revelar información de otros usuarios.
- Se respetarán las convenciones actuales de respuestas, errores y validación.

## 7. Integración con SignalR

El sistema utilizará el building block de SignalR de Akay.To.Azure.

El módulo de notificaciones no deberá implementar nuevamente la infraestructura de conexión, autenticación o publicación si ya está proporcionada por Akay.To.

### Publicación

Los eventos se enviarán a usuarios concretos utilizando su `UserId`.

El identificador empleado por SignalR deberá coincidir con el utilizado en la persistencia.

El sistema admitirá múltiples conexiones simultáneas del mismo usuario.

### Contratos de transporte

Los mensajes publicados incluirán:

- `Type`: identificador estable del tipo de notificación.
- `Payload`: datos específicos del evento.
- `NotificationId`: identificador únicamente para notificaciones persistentes.
- `Title`, `Message` y `Url`: cuando sea persistente.

Los contratos enviados mediante SignalR deberán ser DTOs independientes de las entidades EF Core.

No se expondrán directamente tipos internos de infraestructura.

### Responsabilidades

Akay.To.Azure proporcionará la capacidad técnica de publicación mediante SignalR.

Akay.Be.Notifications determinará qué notificaciones se persisten y cuáles se envían exclusivamente en tiempo real.

## 8. Pruebas

Implementar pruebas unitarias y de integración para verificar:

1. Las notificaciones efímeras se publican sin persistencia.
2. Las persistentes se almacenan antes de publicarse.
3. La implementación utiliza correctamente PostgreSQL y JSONB.
4. Un fallo en la publicación no elimina registros persistidos.
5. Las notificaciones se recuperan paginadas y ordenadas.
6. Los contadores de pendientes son correctos.
7. Marcar como leída una notificación funciona correctamente.
8. El borrado físico elimina únicamente notificaciones del usuario autenticado.
9. No es posible consultar notificaciones de otros usuarios.
10. Las notificaciones admiten una URL opcional.
11. Se pueden enviar mensajes a usuarios con varias conexiones SignalR simultáneas.

## 9. Fuera de alcance

No implementar en esta fase:

- Frontend ni integración con la campanita.
- Eventos concretos de subida o procesamiento de documentos.
- Notificaciones por email, SMS o push.
- Preferencias de usuario.
- Outbox o reintentos persistentes.
- Confirmaciones de recepción.
- Deduplicación automática.
- CorrelationId.
- Sincronización de lectura entre dispositivos.
- Procesos automáticos de limpieza.
- Proyectos adicionales exclusivos para Notifications.

## 10. Instrucciones de implementación

Antes de implementar:

1. Inspeccionar la organización real de Akay.Be.
2. Revisar los building blocks actuales de Akay.To.Azure y Akay.To.EF.
3. Reutilizar los contratos, servicios y convenciones existentes.
4. Adaptar la estructura de carpetas al enfoque Vertical Slice actual.
5. Evitar introducir abstracciones, dependencias y configuraciones duplicadas.

La implementación utilizará .NET 10, EF Core, DI, CancellationToken, logging y observabilidad siguiendo las convenciones existentes.

Mantener el sistema sencillo y extensible, sin desarrollar funcionalidades previstas únicamente para fases futuras.

**Resultado esperado:** sistema backend de notificaciones efímeras y persistentes, integrado con PostgreSQL y Azure SignalR, preparado para ser consumido posteriormente por cualquier funcionalidad de Akay.Be.

### L2 — Authorized architecture change

Assistant proposal: "Recomiendo incorporar `Notification` a Domain, sin dependencias de EF ni SignalR, y mantener contratos y orquestación en Application. Eso conserva las convenciones actuales, pero modifica ese punto de la especificación."
User: "ok, adelante. Bien visto"

### L3 — Implementation boundaries

- Preserve existing untracked docs/specs/. No existing database/schema/credentials or Azure resources will be inspected or modified.
- Receive-only authorized hub, reusing Akay.To configuration and `echo(name,message)` publisher; do not inherit BaseHub's client-invokable cross-user sends.
- Notification persistence must not save pending business changes or publish inside an uncommitted ambient transaction. Save through a fresh DI scope using the existing context registration and IUnitOfWork.
- Payload is a JSON snapshot of the concrete notification (including its additional properties), transported as JSON, stored as jsonb. No queries/indexes over its contents.

### L4 — Focused verification

- `dotnet test test/Akay.Be.Application.Tests/Akay.Be.Application.Tests.csproj --configuration Release --filter FullyQualifiedName~Notifications --no-restore --verbosity quiet -p:RunAnalyzers=false -p:UseLocalAkayTo=true`: 15 passed.
- `dotnet test test/Akay.Be.Infrastructure.Tests/Akay.Be.Infrastructure.Tests.csproj --configuration Release --filter FullyQualifiedName~Notifications --no-restore --verbosity quiet -p:RunAnalyzers=false -p:UseLocalAkayTo=true`: 6 passed against PostgreSQL 17.6, including model-created schema and separately applied reviewed DDL.
- `dotnet test test/Akay.Be.Host.Tests/Akay.Be.Host.Tests.csproj --configuration Release --filter FullyQualifiedName~NotificationApiTests --no-restore --verbosity quiet -p:RunAnalyzers=false -p:UseLocalAkayTo=true`: 11 passed after fixing test harness registration of IHttpContextAccessor. JWT owner delivery and unauthorized REST/hub access checked.
- An initial Infrastructure command without UseLocalAkayTo failed in the sibling Akay.To.EF dependency graph before reaching the new tests. Rerunning with the documented local-reference property reached the intended RED and subsequent GREEN. No dependency source/configuration was changed.
- Testcontainers 4.14's parameterless builder is obsolete; use the image constructor. WithCleanUp(false) only stops containers, so AutoRemove(true) was added; the single stopped test-only container from the first run was removed. No existing development container was touched.
- No meaningful runtime RED applies to passive docs; DDL is functionally exercised only in a newly created disposable database. Initial service/handler RED was missing-type compilation, not claimed as a behavioral failure.
- Final full checks will run with analyzers enabled. A preliminary native assessment requires explicit selection of untracked new files; user-owned docs/specs/ must be excluded from the candidate.
- First analyzer-enabled solution run: 235 tests passed across Domain (72), Application (124), Infrastructure (35), Architecture (4); Host did not run because S6966 rejects synchronous MemoryStream.Write inside the asynchronous WebSocket test helper. Fixed the helper to await WriteAsync; full recheck pending. No containers or scratch paths were left behind.
- Correction recheck: `dotnet test Akay.Be.slnx --configuration Release -p:UseLocalAkayTo=true --no-restore --verbosity quiet` exited 0 with analyzers enabled: Domain 72, Application 124, Infrastructure 35, Architecture 4, Host 22; total 257 passed, zero failed/skipped. No container/scratch leftovers. Independent high-risk verification follows, scoped to items 1 (physical delete), 2 (ownership/auth), 3 (shared DI registration) and 5 (CI image preload).

### L5 — Independent verification and final regression proof

- One independent verifier ran isolated candidate/baseline probes with the same local Akay.To source snapshot and offline cached dependencies. S1-S8 met; no blockers. Rejected mutations preserved SHA-256 data snapshots; publication observed already-committed rows; pending business changes stayed unsaved. Actual JWT service-to-WebSocket delivery reached both owner connections and not another user.
- Existing `POST /api/auth/exchange` behavior matched baseline exactly: status=401, empty body/content-type, www-authenticate=Bearer; differential comparison exited 0. Existing Host/OpenAPI compatibility tests passed on baseline and candidate. Azure/backplane deployment and remote CI were not exercised.
- Adopted the verifier's three executable probes as readable permanent regressions in NotificationBoundaryTests and NotificationApiRegressionTests, without altering production behavior.
- A combined formatter/build/test parent command exceeded its 120s tool timeout, with no test failure reported. No active test hosts remained afterward. The two owned disposable PostgreSQL containers were identified and removed; unrelated preexisting MSBuild node-reuse daemons and development containers were preserved.
- Final standalone full suite with a 600s tool timeout: `dotnet test Akay.Be.slnx --configuration Release -p:UseLocalAkayTo=true --no-restore --verbosity quiet` exited 0; Domain 72, Application 124, Infrastructure 36, Architecture 4, Host 24 = **260 passed, zero failed/skipped**, analyzers enabled. Non-blocking CA warnings remain, including test naming/string-vs-Uri conventions. No test container or test-host leftovers.
- Scoped final whitespace normalization preceded functional verification. No production schema was touched; docs/notifications/schema.sql is reviewed/tested DDL for later manual application only. User-owned docs/specs/ remains untouched. No commits or publishing performed.
- Native review will select only the notification implementation/tests/docs and necessary existing-file integration changes; the local progress logbook and unrelated user-owned specs are excluded from its candidate. No implementation bytes will change after freeze.

### L6 - Authorized identity alignment to int

- User challenged the spec-derived `Guid Id` and `string UserId`: every other Akay.Be entity and FK uses `int`, and the authenticated identity is `int nameid`. User then instructed: "aplica la alineacion completa".
- This explicitly supersedes the original spec table in L1 (Id uuid, UserId text) for implementation. Domain `Notification` is now `Entity<int>` with `int UserId`; the database generates `Id` via PostgreSQL identity.
- Distinction retained: storage and Application use `int`; the only `string` conversion is at the SignalR targeting boundary, required by `ISignalRHubService.SendUserAsync`. REST routes are now `{id:int}`.
- Updated: Domain entity, `INotification`, `NotificationMessage.NotificationId`, repository contract and EF implementation/configuration, handlers, controller routes, DDL (`integer GENERATED BY DEFAULT AS IDENTITY`, `integer` UserId), README, and all notification tests. `Notification.Create` validates `userId > 0`.
- Test-only id assignment for the Host fake repository uses reflection because the in-memory double has no database identity; production ids remain DB-generated and are verified by the PostgreSQL boundary test.
- Verification after the change: `dotnet test Akay.Be.slnx --configuration Release -p:UseLocalAkayTo=true --no-restore --verbosity quiet` exited 0 with Domain 72, Application 124, Architecture 4, Infrastructure 36, Host 24 = **260 passed, zero failed/skipped**. Scoped whitespace normalization applied first.
- Not added (outside requested alignment): foreign key from notifications to the Users table. The spec did not require it; mention as an optional follow-up before production DDL.

### L7 - PostgreSQL snake_case columns

- User correction: "y los campos van en snake_case porque es en postgres". Verified the repository convention first: existing configurations map every column to snake_case (`user_id`, `created_at`, `deleted_at`) and use `IX_Entity_Property` index names.
- Notification columns are now `id`, `user_id`, `type`, `title`, `message`, `url`, `payload`, `created_at`, `read_at`; indexes renamed to `IX_Notification_UserId_CreatedAt` and `IX_Notification_UserId_Unread`, with filter `read_at IS NULL`. DDL `schema.sql` updated accordingly; tests assert the new names and lowercase SQL.
- EF Core identifies an index by its property set, so two indexes over identical columns were merged into one. The unread partial index was narrowed to `(user_id, created_at DESC) WHERE read_at IS NULL` while the general index remains `(user_id, created_at DESC, id DESC)`; both requirements remain covered and the model now contains two indexes.
- Table name remains `notifications` per the original spec, although existing tables are singular (`user`, `center`). Not changed without explicit instruction.
- Infrastructure notification tests: 7 passed after the change.

### L8 - Singular schema/table and preserved PK name

- User moved the database schema and table to singular: `notification.notification`. Configuration, reviewed DDL, and raw SQL used by PostgreSQL tests were aligned; tests now assert `GetSchema()`/`GetTableName()` equal `notification`.
- User reported the primary key constraint keeps its original name `PK_notifications`. DDL now uses that name and the EF model declares `HasKey(...).HasName("PK_notifications")`, so model-created schemas and the reviewed script agree; the DDL test asserts the PK index name too.
- Full suite after both changes: **260 passed, zero failed/skipped**, exit 0.

### L9 - CancellationToken propagated through Akay.To SignalR

- User decided to fix the limitation at its source (option A): every `ISignalRHubService` send/group-management method now takes an optional trailing `CancellationToken`.
- Akay.To changes: `ISignalRHubService.cs` and `AzureSignalRHubService.cs` forward the token to `SendAsync`/`AddToGroupAsync`; `BaseHub` passes `Context.ConnectionAborted`; knowledge and package docs updated. New `AzureSignalRHubServiceTests` verify token propagation for user sends and group joins.
- Akay.Be: `SignalRNotificationPublisher` (now moved to `Infrastructure/Services`) forwards the token and the workaround comment was removed. Test harness now maps the hub explicitly because mapping moved into `InfrastructureRegisterModule.AddHubs`.
- Verification: Akay.To.Azure.Tests 109 passed; Akay.Be 260 passed, zero failed/skipped, exit 0 (local project references).
- Release coordination pending: Akay.Be CI still references `Akay.To.Azure 2.0.5` from NuGet. The new API requires versioning/publishing `Akay.To.Azure` and bumping `Directory.Packages.props`; publishing was not authorized, so it was not done. Note the optional-parameter addition is source-compatible but binary-breaking.
- Cancellation is best effort at transport level: forwarding the token does not guarantee non-delivery.
