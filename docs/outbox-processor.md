# Outbox processor (Akay.Be)

Akay.Be aloja el procesador reutilizable de `Akay.To.EF` sin escribir código propio: el paquete registra
el hosted service que ejecuta `OutboxProcessor<ApplicationDbContext>.RunAsync`.

## Composición

| Lugar | Responsabilidad |
| --- | --- |
| `src/Akay.Be.Infrastructure/InfrastructureRegisterModule.cs` | Registra `AddOutboxProcessor<ApplicationDbContext>(settings, …)`: procesador, señal, contratos y hosted service, con los parámetros tomados de las settings y el ensamblado de dominio como origen de los contratos. |
| `src/Akay.Be.Host/appsettings.json` | `DbContextSettings:ApplicationDbContext:Outbox` con la tabla y su bloque `Processor`, desactivado por defecto. |

El host no registra ningún worker: lo aporta el paquete. La entrega usa el `IMessageBus` ya registrado por
`AddRebusMessaging`. Este proyecto no añade un publicador paralelo ni conoce Rebus.

## Contratos entregables

Hoy los eventos Outbox de Akay.Be solo se persisten: implementan `IOutboxDomainEvent` y **no**
`IOutboxIntegrationEvent`, así que el mapa de contratos del procesador está vacío:

- `AcademicPeriodCreatedOutboxEvent`
- `ExternalIdentityCleanupRequestedOutboxEvent`

Consecuencia operativa: con el mapa de contratos vacío, el procesador marca cada fila con `NotDeliverableAt`
sin consumir intentos ni registrar error, y deja una advertencia en el log. El outbox sigue sirviendo como
registro durable y como costura para consumidores futuros, pero no entrega nada. Cuando estos eventos
implementen `IOutboxIntegrationEvent`, limpie `NotDeliverableAt` con la sentencia de recuperación de
`Akay.To.EF/docs/OutboxProcessor.md` para que el polling los reclame.

El registro recibe el ensamblado que declara esos eventos mediante `DomainAssemblyReference`, un tipo
marcador del propio ensamblado de dominio: el ancla no depende de que un evento concreto siga existiendo
con ese nombre. Si el marcador apuntara a otro ensamblado, el procesador no encontraría los contratos y la
entrega fallaría; por eso `OutboxHostingTests` comprueba los contratos partiendo del marcador.

Los eventos de dominio puramente in-process, como `AcademicPeriodActivatedDomainEvent`, no implementan
`IIntegrationEvent` y nunca se entregan por el bus.

## Activación

`Processor:Enabled` está en `false` en `appsettings.json`. Antes de activarlo:

1. Aplicar el DDL revisable `Akay.To.EF/docs/outbox-schema-postgresql.sql` sobre la base de datos. La tabla
   real es `infra.outbox_message` con columnas en snake_case: el mapeo de EF lo declara con `HasColumnName`
   y el script ya usa esos nombres.
2. Confirmar que el transporte configurado en `MessagingSettings` es durable; `InMemory` no entrega entre procesos.
3. Confirmar que existen consumidores con handlers idempotentes para los contratos anteriores.

Con el procesador desactivado, el worker arranca, registra que está desactivado y termina.

## Operación

- Los parámetros (`PollingInterval`, `BatchSize`, `LeaseDuration`, `MaximumAttempts`, backoff) se documentan
  en `Akay.To.EF/docs/OutboxProcessor.md`.
- Un mensaje con `FailedAt` conserva contenido y error. Se rearáncora con la sentencia documentada allí;
  no hay endpoint ni comando de administración.
- La entrega es at-least-once: los consumidores deben deduplicar por la cabecera `outbox-id`.
