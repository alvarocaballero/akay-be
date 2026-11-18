# Akay.Be | Plan cerrado de implementación: contenidos, revisión e indexación

**Versión 4 (18/11/2026) | Entregable para agente de código**  
**Alcance:** adaptar Akay.Be a los contratos actuales de ConvertToMarkdown, conservando el circuito existente de subida, Outbox, Service Bus, webhook, estados agregados y notificaciones. Este documento es el contrato funcional de esta fase, **no** una autorización para rehacer la arquitectura ni para modificar ConvertToMarkdown salvo dependencia imprescindible y expresamente identificada.

> **Instrucción prioritaria para el agente:** primero inspecciona el código real de Akay.Be y los contratos efectivos del worker/indexador. Reutiliza clases, handlers, nombres, rutas y convenciones existentes. Si un nombre propuesto aquí difiere del código, adapta el plan al código; no crees subsistemas paralelos. Trabaja por fases, ejecuta pruebas y presenta los cambios y las limitaciones verificadas. **No des por implementado algo solo porque aparezca en este plan.**

## 1. Qué existe y NO debe romperse

El flujo actualmente implementado ya permite:

1. Crear un `TopicContent` en `Uploading` y subir el archivo a Blob Storage.
2. Pasar a `Pending`, registrar evento en **Outbox** y publicarlo a Azure Service Bus con el proceso existente (`OutboxProcessor`).
3. Convertir mediante una versión anterior de ConvertToMarkdown, recibir un **webhook** y actualizar estados y errores, correlacionando con **`processing_attempt_id` / `attemptId` ya existentes**.
4. Emitir **notificación no persistente** al finalizar un archivo cuando todavía quedan otros archivos del lote pendientes.
5. Emitir **notificación persistente** cuando ya no quedan archivos pendientes de conversión en el lote.
6. Actualizar estados agregados de `Topic` y comunicar eventos a la UI con las abstracciones actuales.

**Preservar las dos notificaciones y su semántica, sus destinatarios, el Outbox, la publicación, los eventos y el webhook; ajustar únicamente la definición de «conversión terminada», que ahora puede significar `PendingReview` o `Ready`.** Una revisión o un rebuild no equivalen a otra subida y no deben volver a emitir `UploadBatchCompleted` ni duplicar la notificación persistente. Verificar condiciones de carrera entre webhooks concurrentes.

## 2. Modelo de negocio y reglas invariantes

### 2.1. Principal y secundarios

- `TopicContent.IsPrimary` (`bool`, default `false`). **Máximo un principal activo por `Topic`**, también garantizado con índice único parcial PostgreSQL; tener en cuenta `deleted_at` y las convenciones actuales de borrado.
- **Principal:** debe ser un documento (PDF, DOCX, PPTX, MD, TXT y demás formatos documentales soportados); **no** audio, vídeo ni YouTube. No tiene `TopicSectionId`. En la conversión se solicita `extractSections=true`.
- **Secundario:** documento, audio, vídeo o enlace de YouTube. `extractSections=false`. Puede subirse, procesarse y terminar en `Ready` **sin** sección asignada; no bloquear la subida por falta del principal.
- `TopicContent.TopicSectionId` (`int?`): FK opcional de un secundario a una sección **del principal del mismo Topic**. Se puede asignar solo cuando el principal esté en `Ready` y esa sección exista. Un principal nunca debe tener `TopicSectionId`.
- **Indexación:** para cualquiera se requiere `ProcessingState=Ready`; para un secundario, además `TopicSectionId` válido. Si no tiene sección, permanece `Ready/NotIndexed` todo el tiempo que haga falta.
- **Inmutabilidad:** una vez `Ready`, el Markdown ya no puede editarse. La asignación de sección se gestiona aparte y no modifica el MD.
- El Markdown definitivo es la **fuente de verdad de lo que se presenta al alumno y se indexa**. Las páginas originales, coordenadas `bbox` y `charspan` de Docling **no se guardan en las tablas de Akay.Be**; los JSON originales sí se conservan en Blob para diagnóstico y visualización del profesor.
- Secciones y fórmulas se obtienen del **`review.json` definitivo**, no intentando redescubrirlas con un parser de Akay.Be. Para la **navegación por Markdown**, preferir IDs/marcadores estables de sección; nunca confundir `prov[].charspan` de Docling con offsets absolutos en Markdown.

### 2.2. Estados

Como no hay registros actuales, se permite renumerar los valores numéricos, pero **inspeccionar serialización, persistencia, OpenAPI y clientes** antes de hacerlo:

```csharp
public enum ContentProcessingState
{
    Uploading = 1,
    UploadFailed = 2,
    Pending = 3,
    Processing = 4,
    PendingReview = 5,
    Ready = 6,
    Failed = 7
}

public enum ContentIndexingState
{
    NotIndexed = 0,
    Pending = 1,
    Indexing = 2,
    Indexed = 3,
    Failed = 4
}
```

| Escenario | Secuencia |
|---|---|
| A. Con revisión; profesor **modifica** MD | `Uploading → Pending → Processing → PendingReview → Pending → Processing → Ready` |
| B. Con revisión; profesor **no modifica** MD | `Uploading → Pending → Processing → PendingReview → Ready` |
| C. **Sin revisión** al subir | `Uploading → Pending → Processing → Ready` |
| Error de subida | `Uploading → UploadFailed` |
| Error de worker/finalización | `Processing → Failed` después de agotar política de reintentos; errores recuperables no deben destruir información útil |

Para YouTube (sin fichero subido) comenzar desde `Pending` y registrar Outbox, sin simular `Uploading` a Blob. `PendingReview` significa **conversión terminada pero aprobación humana pendiente**. Los estados agregados de `Topic` deben considerar `PendingReview` distinto de `Ready`, aunque para **notificación de conversión terminada** ya no esté pendiente el worker.

### 2.3. Opciones de extracción

En la petición de creación/subida el usuario elige: `requiresReview` (**default `true`**), `extractFormulas`, `extractCode`, `isPrimary`, `sectionId?` y origen **documento / audio / vídeo / URL YouTube**. `extractSections` **no** es opción del cliente: `extractSections = isPrimary`.

`requiresReview` **no necesita columna en BD**: viaja con el mensaje de Service Bus y el worker debe devolverlo por webhook, o recuperarse mediante correlación del intento existente. **Confirmar este contrato real**; no deducirlo del `review.json` (el de vídeo ni siquiera tiene `processing.operation`). En el rebuild siempre vale `false`.

`extractFormulas` y `extractCode` **sí deben permanecer recuperables después de la conversión**, porque una aprobación con cambios puede producir `RebuildFromMarkdown` días después. Preferir guardarlos como opciones de `TopicContent` si no existe ya una fuente persistente fiable; no confiar en memoria, request HTTP ni metadatos hipotéticos. Adaptar los nombres exactos de los campos al contrato vigente del worker. Para audio/vídeo, validar explícitamente qué opciones aplican.

## 3. Persistencia: esquema mínimo de PostgreSQL

### 3.1. `academic.topic_content`

Añadir `is_primary boolean NOT NULL DEFAULT false` y `topic_section_id integer NULL`. Conservar `processing_attempt_id`, errores, estados, rutas y resto de campos existentes. Si procede, añadir las dos opciones persistentes `extract_formulas` / `extract_code` (`boolean NOT NULL DEFAULT false`). Reutilizar los campos existentes que identifiquen origen y tipo; para YouTube guardar fuente/URL canónica de forma segura **sin fingir un blob de origen**.

```sql
CREATE UNIQUE INDEX ux_topic_content_primary_active
    ON academic.topic_content (topic_id)
    WHERE is_primary = TRUE AND deleted_at IS NULL;
```

Validar en servicio y reforzar por FK o constraint apropiada que `topic_section_id` corresponda al mismo Topic y a su principal. Evaluar FK compuesta a `(id, topic_id)` si encaja con el esquema y las convenciones. Evitar una referencia a una sección de otro Topic o de contenido eliminado. Los scripts/migraciones deben seguir la numeración y el procedimiento actuales de Akay.Be: **no ejecutar migraciones desde `Program.cs`**.

### 3.2. `academic.topic_section`

Campos mínimos recomendados:

| Campo | Fuente |
|---|---|
| `id` int PK | BD |
| `topic_id` int FK | `TopicContent.TopicId` |
| `topic_content_id` int FK | Contenido **principal** |
| `source_section_id` varchar/text | `review.sections[].id` (UUID/string del worker) |
| `parent_section_id` int NULL FK | Resolver `review.sections[].parentId` dentro del mismo principal |
| `title` text | `review.sections[].title` |
| `level` int | `review.sections[].level` |
| `display_order` int | `review.sections[].order` (puede ser 0) |
| `markdown_anchor` text NULL | Identificador/marcador de navegación correspondiente al Markdown final, si se confirma en el worker |
| `docling_item_ref` text NULL | Opcional, solo trazabilidad hacia artefactos; **no** para calcular offsets Markdown |

Índice único `(topic_content_id, source_section_id)` e índices de consulta por `topic_id`/orden. Crear primero todas las secciones y después resolver padres; validar identidad, jerarquía y ciclos. El Markdown con `extractSections=true` puede incluir marcadores `<!-- akay-section:start id="..." -->`: confirmar su emisión y correspondencia con `source_section_id` para navegar. **No almacenar offsets de Docling como si fueran del Markdown.** Si el marcador esperado no existe, dejar ubicación sin resolver y registrar la limitación; no inventar posiciones. No convertir esa ausencia en error de conversión salvo que el contrato exija explícitamente marcadores.

### 3.3. `academic.topic_formula`

Campos mínimos recomendados:

| Campo | Fuente |
|---|---|
| `id` int PK | BD |
| `topic_id` int FK | Contenido |
| `topic_content_id` int FK | Contenido que contiene la fórmula (principal **o** secundario documental) |
| `source_formula_id` varchar/text | `review.formulas[].id` |
| `text` text NULL | `review.formulas[].text` |
| `latex` text NULL | `review.formulas[].latex` (**puede ser null**) |
| `source_section_id` varchar/text NULL | `review.formulas[].sectionId`, si existe; referencia de **origen**, no PK |
| `topic_section_id` int NULL FK | Solo cuando `source_section_id` se resuelve a sección del principal del Topic |
| `docling_item_ref` text NULL | Opcional para navegación hacia artefactos, no como offset Markdown |

Índice único `(topic_content_id, source_formula_id)`. En el principal, resolver el `sectionId` de origen al PK de la sección recién persistida. En un secundario documental `extractSections=false`, normalmente no hay IDs de sección propios: **no confundir** `review.formulas[].sectionId` con el `topic_content.topic_section_id` del secundario; si se desea usar esta última como contexto de la fórmula, documentar esa regla de forma explícita. No descartar fórmulas con `latex=null`.

**No crear** `topic_table`, `topic_image` ni `topic_segment` en esta fase. `review.tables`, `review.images` y `transcript.segments` permanecen en Blob, y la API los expone. Las tablas/imágenes del ejemplo no incluyen `sectionId`: por ahora no inventar una relación con sección; se presentan y se localizan mediante el Markdown y sus enlaces/artefactos. Si después se necesita asociación formal, requerirá un contrato explícito del worker o una fase posterior.

## 4. Artefactos y contratos reales proporcionados

### Documento (Docling)

`review.json` aporta identidad, estructura, fórmulas y diagnósticos; no proporciona offsets absolutos del Markdown. Ejemplo representativo:

```json
{
  "schemaVersion": 1,
  "contentKey": "topic-content:501",
  "contentType": "document",
  "summary": {"pageCount":3,"sectionCount":2,"tableCount":1,"imageCount":2,"formulaCount":2,"durationMilliseconds":null},
  "sections": [
    {"id":"c1a3cc1f-4beb-5058-ba1e-51af764fccd7","parentId":null,"title":"Fractions","level":1,"order":0,"doclingItemRef":"#/texts/0","pageNumber":1},
    {"id":"4c8d1f14-ffda-5b2e-9616-c7a4f82e34ef","parentId":"c1a3cc1f-4beb-5058-ba1e-51af764fccd7","title":"Simplifying","level":2,"order":1,"doclingItemRef":"#/texts/2","pageNumber":2}
  ],
  "formulas": [
    {"id":"formula-001","doclingItemRef":"#/texts/4","sectionId":"4c8d1f14-ffda-5b2e-9616-c7a4f82e34ef","text":"a / b","latex":"\\frac{a}{b}","pageNumber":2},
    {"id":"formula-002","doclingItemRef":"#/texts/5","sectionId":"4c8d1f14-ffda-5b2e-9616-c7a4f82e34ef","text":"x plus unreadable glyph","latex":null,"pageNumber":3}
  ],
  "tables": [{"id":"table-001","doclingItemRef":"#/tables/0","cells":[{"text":"Expression","bbox":null}],"blobName":"topics/25/content/501/lesson.artifacts/images/table-001.png"}],
  "images": [{"id":"figure-001","blobName":"topics/25/content/501/lesson.artifacts/images/figure-001.png","pageNumber":3}],
  "confidence": {"meanGrade":"fair","lowGrade":"fair","pages":[{"pageNumber":3,"tableScore":null,"tableGrade":null,"meanGrade":"poor"}]},
  "warnings": [{"code":"IMAGE_NOT_MATERIALIZED","message":"An image has no local crop; linked resources were not fetched.","pageNumber":3}],
  "processing": {"operation":"convert","engine":"docling","engineVersion":"2.141.0"},
  "youtube": null,
  "extractionReviewBlobName": null
}
```

`document.docling.json` contiene procedencias del documento original y puede incluir `prov` con `page_no`, `bbox`, `charspan`, pero **no deben persistirse como coordenadas del contenido educativo**:

```json
{"self_ref":"#/texts/0","label":"text","prov":[{"page_no":2,"bbox":{"l":10.5,"t":20.25,"r":80.0,"b":32.75,"coord_origin":"TOPLEFT"},"charspan":[0,10]}],"orig":"Hola mundo","text":"Hola mundo"}
```

Tras `RebuildFromMarkdown` no hay necesariamente páginas ni `prov`: `pageNumber=null`, `prov=[]`. Esto **no es error**. Mantener ambos JSON íntegros en Blob y disponibles vía API para inspección.

### Audio, vídeo y YouTube

`review.json` para vídeo: `contentType="video"`, `sections:[]`, `formulas:[]`, `tables:[]`, `images:[]`, `summary.durationMilliseconds`, `processing.engine="azure-speech-fast-transcription"`. **No hay `document.docling.json`**. Para YouTube `source.type="youtube"` y `youtube={videoId,canonicalUrl,title}`.

`transcript.json` separado:

```json
{
  "schemaVersion":1,"contentKey":"topic-content:502",
  "origin":{"type":"blob","blobName":"topics/25/content/502/original.mp4"},
  "language":"es-ES","durationMilliseconds":18500,
  "segments":[
    {"id":"segment-0001","startMilliseconds":0,"endMilliseconds":9000,"speaker":"0","language":"es-ES","text":"Welcome to the lesson."},
    {"id":"segment-0002","startMilliseconds":9000,"endMilliseconds":18500,"speaker":"0","language":"es-ES","text":"Let us compare two fractions."}
  ]
}
```

**Información ya verificada por el agente del worker:** la indexación de transcripciones conserva en Qdrant `contentKey`, `sectionId`, origen Blob/YouTube y `provenance.segments` con `id`, `startMilliseconds` y `endMilliseconds`. El agente afirma haber ejecutado 16 pruebas, incluidas división/unión de chunks y referencias a Qdrant local. **Akay.Be debe aprovechar el indexador existente; no desarrollar otro chunker ni crear tablas de segmentos.** La navegación de citas hacia la sección y el reproductor con timestamp es integración de Akay.Be/frontend posterior, no está demostrada por esas 16 pruebas.

Si Speech no devuelve timestamps, pueden ser `null`. Un chunk puede contener varios segmentos. **No interpolar tiempos por palabra.** Si se modifica el Markdown de una transcripción, no afirmar que sus segmentos originales siguen alineados: verificar soporte del worker. En esta fase, por seguridad, permitir aprobar transcripciones sin modificar; habilitar reconstrucción de transcripciones editadas únicamente si el worker garantiza preservación/correspondencia de tiempos. Documentar esa limitación, sin bloquear subida, revisión ni indexación de audio/vídeo no modificados.

### Error

El siguiente objeto **es un webhook `document.conversion.failed`, NO un `review.json`**:

```json
{
  "schemaVersion":1,"requestId":"8db8b749-d1b1-49ef-8431-db5ee9d003f6",
  "contentKey":"topic-content:503","payload":{},
  "error":{"code":"DOCUMENT_CONVERSION_FAILED","message":"The document could not be converted completely."},
  "eventType":"document.conversion.failed","source":{"type":"blob","blobName":"topics/25/content/503/broken.pdf"},
  "destinationBlobName":"topics/25/content/503/broken.md","contentType":"document","operation":"convert"
}
```

En error, no buscar ni interpretar `review.json`; validar identidad/correlación, registrar `processing_error` y conservar el comportamiento de notificación ya existente. El ejemplo no exhibe `attemptId` ni `requiresReview`: localizarlos en el **envoltorio/contrato real**, sin inventarlos en el JSON mostrado.

## 5. Contratos HTTP (adaptar rutas al estilo existente)

### 5.1. Ampliar subida/creación existente

Campos orientativos de request (no crear un segundo flujo de subida):

```json
{
  "sourceType":"document",
  "isPrimary":false,
  "sectionId":null,
  "requiresReview":true,
  "extractFormulas":false,
  "extractCode":false
}
```

- `sourceType`: `document | audio | video | youtube`; **exactamente una fuente** entre fichero local y URL de YouTube. Validar extensión/MIME para ficheros y URL YouTube según contrato real del worker; evitar SSRF y orígenes no autorizados.
- Principal solo documento; `sectionId` nulo. Secundario acepta `sectionId=null` al subir; si se pasa un ID, validar principal `Ready` y pertenencia exacta al mismo Topic.
- `requiresReview` por defecto `true` cuando se omite. `extractSections` derivado exclusivamente de `isPrimary`.
- Cualquier modalidad de subida ya soportada (creación previa + SAS/subida a Blob + confirmación) debe mantenerse; para YouTube crear `Pending` y Outbox sin fingir fichero.

### 5.2. Editor/inspector de contenido

| Endpoint orientativo | Resultado |
|---|---|
| `GET /api/topic-contents/{id}/markdown` | Markdown del Blob vinculado y `ETag`/versión para edición. |
| `PUT /api/topic-contents/{id}/markdown` | Sobrescribe **el mismo blob** exclusivamente en `PendingReview`, con `If-Match`/ETag y autorización. |
| `GET /api/topic-contents/{id}/review` | `review.json` **completo**, incluidas `sections`, `formulas`, `tables`, `images`, `confidence`, `warnings`, `summary`, `youtube`, sin perder campos desconocidos. |
| `GET /api/topic-contents/{id}/docling` | `document.docling.json` cuando existe; ausencia documentada para audio/vídeo/YouTube. |
| `GET /api/topic-contents/{id}/transcript` | `transcript.json` con segmentos y timestamps, si existe. |
| `GET /api/topic-contents/{id}/assets/{assetId}` | Imagen/tabla autorizada por pertenencia al `TopicContent`, como stream o SAS de lectura corta. |

Reutilizar la capa Blob del proyecto y resolver nombres **desde el contenido/contrato del worker**, no a partir de rutas arbitrarias proporcionadas por el cliente. No habilitar contenedores públicos. JSON de revisión/diagnóstico permanece en Blob, no en columnas PostgreSQL. La API puede devolver el JSON original sin forzar DTOs que descarten atributos futuros. Autorización profesor/admin para editar y acceder a diagnósticos; alumno solo a contenido publicado/autorizado conforme al modelo vigente.

### 5.3. Aprobación

`POST /api/topic-contents/{id}/approve` con `{ "markdownModified": true | false }`. Solo desde `PendingReview` y con permisos apropiados.

- **B: `false`**: no invocar worker; leer `review.json` definitivo, persistir secciones/fórmulas cuando proceda y pasar a `Ready` en una **misma transacción**. No generar nueva conversión ni nuevo evento de lote.
- **A: `true`**: el MD ya está editado en su mismo Blob. En una transacción, generar **nuevo `processing_attempt_id`**, cambiar a `Pending` y registrar Outbox para `RebuildFromMarkdown` con `requiresReview=false`. El worker conserva el Markdown y sustituye `review.json`, `docling.json` e imágenes. Al llegar el webhook correcto, persistir metadatos finales y pasar a `Ready`.
- **C: sin revisión desde subida**: al webhook exitoso de conversión inicial, ejecutar **la misma rutina de finalización** que el caso B; no necesita llamar al endpoint de aprobación.

No aprobar un contenido durante procesamiento/rebuild ni permitir actualizar el Markdown en `Ready`. La API debe proteger edición vs. aprobación concurrentes: `ETag` de Blob y validación transaccional del estado; no asumir que Blob+PostgreSQL participan en una única transacción distribuida.

### 5.4. Asignación de sección

`PATCH /api/topic-contents/{id}/section` con `{ "sectionId": 123 }` (opcionalmente `null` para desasignar según reglas). Solo secundarios, con principal `Ready` y sección válida del mismo Topic. Se puede asignar más tarde a un secundario `Ready` sin alterar su Markdown.

**Política V1 segura:** si el secundario está `Pending`/`Indexing`/`Indexed` en `ContentIndexingState`, bloquear reasignación/desasignación. Se permite estando `NotIndexed` o `Failed`, siempre que no exista indexación activa ni puntos vigentes de una indexación anterior; comprobar esta última condición antes de habilitarla. Así evitamos payloads Qdrant con `sectionId` obsoleto. Una futura operación explícita de desindexado/reindexado podrá levantar esta restricción.

### 5.5. Solicitar indexación

`POST /api/topic-contents/{id}/index`:

- Requiere `ProcessingState=Ready` y, si es secundario, sección válida. Verificar permisos, no permitir indexaciones simultáneas ni reindexar implícitamente un `Indexed`.
- En **transacción PostgreSQL**: cambiar a `IndexingState=Pending`, registrar Outbox del proceso de indexación existente y usar correlación/idempotencia compatible con él (reutilizar intento existente o añadir `indexing_attempt_id` **solo si es necesario**).
- El worker/indexador existente realiza `Pending → Indexing → Indexed|Failed`. `Indexed` **solo** tras confirmación efectiva; no simular resultado ni implementar un indexador alternativo en Akay.Be.
- Para secundarios, el `sectionId` enviado al indexador debe proceder de **la asignación actual y validada** de BD, no del momento de subida. Verificar si el contrato espera PK interna de sección o `source_section_id` y mapearlo explícitamente; nunca confundirlo con `review.formulas[].sectionId`.
- El payload Qdrant de vídeo/audio ya conserva `provenance.segments`; **no duplicarlo en PostgreSQL**. La resolución futura de citas (contenido, sección, permisos de reproducción y posición temporal) queda fuera de esta fase salvo interfaces preexistentes reutilizables.

## 6. Service Bus y webhooks

Reutilizar el mensaje de conversión existente (`userId`, `contextId`, `documentId`, `fileName`, `sourceBlobName`, `callback`, `attemptId`, etc.). Adaptar el contrato real con estos datos lógicos:

| Campo | Conversión inicial | Rebuild tras edición |
|---|---|---|
| `operation` | `convert` (o valor real) | `RebuildFromMarkdown` (o valor exacto del worker) |
| `sourceBlobName` | Archivo original en Blob | **Markdown revisado** |
| `destinationBlobName` | Según contrato de convert | **Omitido** |
| `requiresReview` | Opción de subida, default `true` | **`false`** |
| `extractSections` | `isPrimary` | `isPrimary` |
| `extractFormulas`, `extractCode` | Opciones elegidas | Opciones originales recuperadas |
| `attemptId` | Vigente | **Nuevo intento** |
| Fuente YouTube | URL/tipo admitidos, no blob ficticio | Solo si realmente existe soporte y es necesario |

El caso A usa **`RebuildFromMarkdown` con `sourceBlobName` y sin `destinationBlobName`**, y ese contrato mantiene el mismo MD reemplazando los artefactos vinculados. No inventar un destino nuevo ni volver a OCR/transcribir el origen para corregir Markdown.

El webhook debe validar autenticación, `contentKey`/`documentId`, `attemptId` vigente, `operation` y resultado. En éxito de **convert**:

- `requiresReview=true` → `PendingReview` (sin persistir aún secciones/fórmulas definitivas).
- `requiresReview=false` → rutina compartida de finalización → `Ready`.

En éxito de **rebuild** → rutina compartida de finalización → `Ready`. En error → guardar error según política existente. Si el webhook real no devuelve `requiresReview`, añadir propagación mínima en el worker o usar correlación durable por intento existente; **no introducir por defecto una columna `requires_review`** sin probar antes la alternativa pactada.

## 7. Finalización atómica, reintentos e idempotencia

Implementar/reutilizar un **único servicio de finalización**, invocado por el webhook sin revisión y por `approve(false)`. No duplicar reglas entre handlers.

1. **Fuera de la transacción:** localizar y leer el `review.json` **definitivo** de Blob; validar `schemaVersion` soportada, `contentKey`, tipo, que el contenido esperado es el actual y que los arrays de secciones/fórmulas permiten cero elementos. No es obligatorio leer `document.docling.json` para persistir secciones/fórmulas, ya que las coordenadas/páginas originales no forman parte del esquema. Conservarlo solo para endpoint de diagnóstico.
2. Construir un modelo interno de finalización desde `review.json`: para **principal**, secciones + fórmulas de documento; para **secundario documental**, fórmulas si fueron solicitadas; para audio/vídeo, no se crean estas filas. Tolerar `latex=null`, `warnings`, `confidence` pobre y campos futuros: **no** convertir una advertencia de extracción en `Failed` automáticamente.
3. **En transacción PostgreSQL**, bloquear el `TopicContent`/verificar versión e intento/estado; insertar o sustituir idempotentemente la estructura definitiva, enlazar padres y fórmulas, cambiar a `Ready` y actualizar estado agregado del Topic. Registrar en el Outbox cualquier evento necesario bajo las reglas existentes. **Nunca** `Ready` si faltó persistir las tablas requeridas.
4. Repetir webhook o aprobación para el mismo intento debe resultar idempotente: **sin filas duplicadas ni notificación persistente duplicada**. La clave de idempotencia debe combinar identidad de contenido + intento/operación en función de los mecanismos existentes. Webhooks de intentos antiguos no pueden modificar estado ni metadatos actuales.
5. **Errores transitorios:** permitir retry seguro de lectura de Blob, BD, publicación vía Outbox y finalización; no marcar `Failed` por la primera excepción recuperable. Errores definitivos: registrar detalle y transicionar conforme al sistema existente. Nunca confirmar HTTP de webhook antes de asegurar persistencia/aceptación durable, según contrato y reintentos reales.
6. **Consistencia Blob/DB:** no hay transacción distribuida. Verificar si ConvertToMarkdown impide que un intento antiguo sobrescriba artefactos vigentes y si los blobs se leen con versión/ETag estable. `attemptId` evita callbacks antiguos, pero **por sí solo no impide escrituras antiguas en Blob**. Si se detecta riesgo real, introducir protección mínima y documentar cualquier cambio necesario del worker. Un MD editado no debe cambiar durante finalización.

**Notificaciones originales:** en la conversión inicial, cuando todavía quedan otros archivos pendientes → notificación no persistente; cuando termina el último → notificación persistente. `PendingReview` cuenta como **worker terminado** para ese cálculo, pero no como `Ready` del Topic. Rebuild, aprobación, asignación de sección e indexación **no** vuelven a completar el lote. Asegurar que dos callbacks simultáneos no emiten dos notificaciones persistentes.

## 8. Fases ejecutables y pruebas de aceptación

### Fase 0: auditoría rápida de implementación

Entregar un mapa breve de entidades/handlers/endpoints actuales, Outbox, webhook, notificaciones, servicios Blob, esquema PostgreSQL y contratos **reales** de worker e indexador. Confirmar ubicación de `review.json`, `document.docling.json`, `transcript.json` y assets por los datos reales del callback/nomenclatura. Confirmar opciones de fórmulas/código, YouTube, `RebuildFromMarkdown` y correlación de `requiresReview`. **No rediseñar nada que ya funcione.**

### Fase 1: dominio y BD

Enums/estados, campos `IsPrimary` y `TopicSectionId`, opciones de extracción si hacen falta, `topic_section` y `topic_formula`, FKs, índices y scripts SQL manuales. Pruebas de principal único, restricciones, jerarquía y relaciones cruzadas.

### Fase 2: adaptar subida, contrato worker y webhook

Conservar Uploading/Outbox/Service Bus/notificaciones. Añadir modalidades de fuente, flags, transición `PendingReview` y finalización automática sin revisión. Implementar parser robusto de `review.json` y servicio compartido de finalización. Casos error, duplicados y concurrencia.

### Fase 3: pantalla de revisión y aprobación

GET/PUT Markdown, GET `review`, `docling`, `transcript`, assets con autenticación y ETag; `approve(false)` y `approve(true)` + rebuild sin destino. Congelación de MD al aprobar; asignación posterior de sección; tests de permisos e idempotencia.

### Fase 4: indexación y compatibilidad Qdrant

Endpoint de indexación y conexión **al indexador existente**, propagación correcta de `sectionId`, estados `NotIndexed/Pending/Indexing/Indexed/Failed`, reintentos; verificar que los segmentos y tiempos llegan intactos al payload, aprovechando la implementación ya probada. No añadir chunker alternativo.

### Fase 5: integración real y regresión

Ejecutar pruebas existentes más casos reales contra worker/Blob/Service Bus/Qdrant disponibles. No bloquear todo el proyecto por campos de diagnóstico no utilizados. Documentar de forma reproducible diferencias reales entre los fixtures proporcionados y los outputs efectivos.

**Casos de aceptación mínimos:**

1. A (`PendingReview` + Markdown editado → `Pending/Processing` + rebuild + webhook → `Ready`), B (`PendingReview` + sin cambios → `Ready` directo), C (`convert` sin revisión → `Ready` directo). Ninguno duplica procesamiento o notificaciones.
2. Secundarios documental, audio, vídeo y YouTube: subida sin sección permitida; indexación prohibida sin sección; asignación correcta tras principal `Ready`.
3. Solo un principal por Topic; nunca multimedia ni URL YouTube; `extractSections=true` solo en principal.
4. Secciones anidadas del review: `parentId`, `level`, `order=0`, IDs estables y título. Fórmulas con `latex=null`; relación de sección cuando sea resoluble; los secundarios **no generan** secciones.
5. `document.docling.json` puede faltar en vídeo; `prov=[]` y páginas `null` tras rebuild: **no** impiden `Ready`. No se persisten ni confunden `bbox`/`charspan` con offsets del Markdown.
6. `review.json` completo recuperable: imágenes, tablas, `warnings`, `confidence` (incluido `tableScore=null`), `youtube`; `transcript.json` con segmentos; imágenes/tabla accesibles sin exposición pública.
7. Error `document.conversion.failed` tratado como **evento** y no review; preservación de estado/error y notificaciones; duplicados/antiguos no corrompen DB.
8. Dos webhooks concurrentes, redelivery Service Bus, reintentos Outbox, fallo al insertar una fórmula tras crear secciones: **rollback** e idempotencia.
9. Lote de N archivos con mezcla `PendingReview`, `Ready` y `Failed`: misma distinción anterior entre notificación temporal y persistente; una sola persistente de lote; el rebuild no crea otro lote.
10. Acceso no autorizado a Markdown, JSON o imagen denegado; PUT con ETag inválido denegado; PUT cuando `Ready`/`Pending`/`Processing` denegado.
11. Indexación de secundario usa **su sección actual** y el indexador conserva `contentKey`, `sectionId`, origen y `provenance.segments` (varios segmentos, valores de tiempo `null` cuando existan). `Indexed` solo tras confirmación.
12. Revisión audiovisual con cambios: no se permite perder o inventar alineación temporal; documentar soporte real antes de permitir esta operación.

## 9. Entregables del agente

- Código de Akay.Be modificado siguiendo patrones del proyecto (Vertical Slice/CQRS/DDD, repositorios y Outbox actuales).
- Scripts SQL versionados y configuración necesaria, sin migración automática al arrancar.
- Tests unitarios/de integración y **resultado real** de su ejecución.
- Resumen conciso: clases afectadas; contratos finales HTTP/Service Bus/webhook; comportamiento preservado de notificaciones; compatibilidad comprobada con ConvertToMarkdown; limitaciones **reales** (si las hay) y pasos de prueba manual del frontend.
- No cambios de arquitectura ajenos al alcance; no implementar UI React, reproductor de vídeo, chat RAG o navegación de citas en esta fase.

### Decisiones expresamente pospuestas, NO bloqueantes

- Persistir tablas o imágenes en PostgreSQL, o inventar su `sectionId` cuando `review.json` no lo aporta.
- Guardar posiciones originales de PDF (`bbox`, `charspan`, página) en la BD educativa.
- Navegación completa desde la cita RAG a reproductor con timestamp y a la sección Markdown (Qdrant ya aporta datos; falta integración UI/API).
- Reasignar la sección de un contenido ya indexado: requiere desindexación/reindexación segura.
- Asegurar timestamps fiables tras reescribir una transcripción, sin soporte explícito probado del worker.
- Rediseñar el modelo de blobs, rehacer notificaciones o implantar otro motor de indexación.

**Principio final:** implementar y probar lo existente y verificable. Ante diferencias entre supuestos y outputs reales de Docling/Speech, preservar información, registrar la limitación y evitar fabricar offsets, secciones, fórmulas o tiempos. El objetivo de esta fase es **que Akay.Be pueda subir, convertir, revisar y dejar listo para indexar contenidos reales, sin romper lo ya construido**.
