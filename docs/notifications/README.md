# Backend notifications

Use `INotificationService` from Application after committing the business operation. An `INotification` is sent only in real time; an `IPersistentNotification` is committed to PostgreSQL first and remains queryable if real-time publication fails. Delivery and reading are not guaranteed.

## Integration

1. Review [schema.sql](schema.sql) and apply it manually to the intended PostgreSQL database before deploying the endpoints. There are no EF migrations or automatic production schema changes.
2. Inject `Akay.Be.Application.Notifications.INotificationService` into the use case that produces a notification. Set `UserId` to the local Akay user ID (`int`), **not** the external Entra ID.
3. Implement `INotification` for transient messages, or `IPersistentNotification` for stored messages. Call `SendAsync(notification, cancellationToken)` after the business commit, outside an ambient transaction.

The implementation reuses the existing `ApplicationDbContext`, Akay.To EF unit of work, dispatcher, validation, HTTP Result mapping, user context, and Akay.To.Azure SignalR publisher. Notification commits use a fresh DI scope so pending tracked business entities are never saved implicitly. Domain contains the Notification entity, as explicitly approved during implementation.

## REST contract

All endpoints require authentication and derive ownership exclusively from the authenticated local user. There is no client-selectable user or tenant parameter.

| Method | Route | Response |
|---|---|---|
| GET | `/notifications?Page=1&PageSize=20&IsRead=false` | Standard `PagedResponse<List<NotificationResponse>>` |
| GET | `/notifications/unread-count` | JSON integer |
| PATCH | `/notifications/{id}/read` | 204, or 404 for missing/not-owned |
| PATCH | `/notifications/read-all` | 204 |
| DELETE | `/notifications/{id}` | 204, or 404 for missing/not-owned |

- Page defaults to 1; page size defaults to 100 and must be 1–100. Invalid/overflowing offsets return 400.
- Omit `IsRead` for all messages, use `false` for unread, or `true` for read.
- Ordering is always `CreatedAt DESC, Id DESC`; `SortBy=CreatedAt` and `IsAscending=false` are the only supported explicit ordering values.
- The first `ReadAt` timestamp is preserved on repeated reads, including `read-all`. DELETE is physical, not soft delete.
- Both foreign and missing IDs produce the same not-found error. No center membership is required for a user's own notifications.
- Response payloads are JSON objects, not escaped JSON strings. URLs may be null and must otherwise be internal application paths.

## SignalR contract

Connect to `/hub/notifications` using an Akay JWT obtained through the existing token exchange. The hub uses the existing authentication pipeline (including query `access_token` support on `/hub`) and CORS policy. All connections of the same local user receive the directed event.

The existing Akay.To directed client event is **`echo`**, with two arguments: `name` (normally `_SYSTEM_`) and the notification DTO:

```json
{
  "type": "application.notice",
  "payload": { "userId": 12, "type": "application.notice", "detail": "An update is available" }
}
```

Persistent messages additionally carry `notificationId` (integer), `title`, `message`, and `url` when non-null. Ephemeral messages omit persistence metadata. `Type` is the stable application discriminator; it is not a SignalR method name.

Storage keeps the local `int` user identity and a database-generated `int` notification identity, consistent with the rest of the application. Only the SignalR targeting call converts the user id to its invariant string form. Columns follow the project's PostgreSQL `snake_case` convention (`id`, `user_id`, `created_at`, `read_at`).

`Payload` is the JSON snapshot of the concrete notification's public serializable properties, including additional fields even when sent through `INotification`. The same snapshot is stored as JSONB for persistent messages. Producers must include only intended transport data; no EF entities or provider-internal types belong in notification contracts. No JSON-property querying or JSONB indexes are provided.

The hub is receive-only: unlike the reusable demo `BaseHub`, it exposes no client-callable cross-user publishing or group-joining methods. Authentication, connection management and publication still come from ASP.NET Core and Akay.To; no parallel transport infrastructure is introduced.

## Failure and cancellation boundaries

- Serialization/validation or persistence failure prevents publication and propagates to the caller. Callers must not retry an already committed business operation just because notification creation failed.
- Publication failure is logged as a structured warning, without a payload dump. Stored notifications are neither removed nor persisted again. Ephemeral messages may be lost.
- Cancellation before work starts is honored. Once persistence commits, the token is forwarded to the SignalR send; a publication failure caused by cancellation is logged as best effort and the stored record remains available.
- `Akay.To.Azure` forwards the token to SignalR (`SendAsync(..., cancellationToken)`). Transport cancellation is best effort: a canceled token does not guarantee the message was not delivered.
- No Outbox, persistent retries, delivery receipts, deduplication, correlation columns, read synchronization or retention jobs are added. The application's existing unrelated Outbox configuration is unchanged.

## Verification

Run with the existing local Akay.To references:

```powershell
dotnet test Akay.Be.slnx --configuration Release -p:UseLocalAkayTo=true
```

PostgreSQL tests require Docker and a locally available **`postgres:17.6`** image. They never pull images implicitly; CI prepares the image explicitly. Tests create a disposable container and a new database per case, build the schema from the current EF model, and separately verify the reviewed DDL. No application connection string, existing database or Azure credential is used. The container is automatically removed on disposal. Missing Docker/image is a test failure, not a silent skip.

Host tests use TestServer with real Akay.To-issued JWTs and the existing user context, plus a repository test double. PostgreSQL storage/ownership behavior is independently covered by real-provider tests. The SignalR test exercises the actual Akay.To publisher over three real WebSocket connections: two for one user and one for another.

Azure SignalR Service itself is not contacted by local tests. Its existing optional backplane registration is reused unchanged; validating a deployed Azure instance requires separately authorized environment access.
