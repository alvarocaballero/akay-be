# AGENTS.md - Akay.Be

`Akay.Be` currently validates the `Akay.To.*` packages and is evolving into the production API for the Akay educational platform. Do not treat it as a disposable test project.

> **Before implementing infrastructure or cross-cutting concerns, always inspect the Akay.To architecture knowledge index and reuse an existing Akay.To building block when available.**

## Build

```powershell
.\build-local.cmd   # Uses local ProjectReference when ../Akay.To exists
.\build-ci.cmd      # Forces PackageReference
dotnet test Akay.Be.slnx --configuration Release
```

## Structure

* Solution: `Akay.Be.slnx`
* Source: `src/`
* Tests: `test/`
* Entry point: `src/Akay.Be.Host/Program.cs`

## Architecture

```text
Host → Application, Infrastructure
Application → Domain
Infrastructure → Application, Domain
Domain → none
```

* Application must not reference Infrastructure.
* Controllers and consumers remain thin and delegate through the dispatcher/mediator.
* Organize Application by feature or use case using Vertical Slice Architecture.
* Repositories return domain entities.
* Mapping to responses belongs in Application.
* EF Core configuration and repositories belong in Infrastructure. This project does not use EF Core migrations; the database schema is managed manually and must be kept aligned with the current model.
* Domain must not depend on EF Core, HTTP, Azure, AI providers or other infrastructure concerns.

## Domain

* Aggregate roots inherit from `AggregateRoot<int>`.
* Child entities inherit from `Entity<int>`.
* Use domain methods and avoid public setters.
* Protect invariants in Domain or Application.
* Educational resources and exercises require `TopicId`.
* `SectionId` is optional and, when present, must belong to the same topic.

## Akay.To Building Blocks

`Akay.Be` uses `Akay.To.*` packages as its reusable application and infrastructure building blocks.

Current package families include:

* `Akay.To.Core`
* `Akay.To.Dispatcher`
* `Akay.To.EF`
* `Akay.To.Azure`
* `Akay.To.AI`

This list may evolve. Do not assume it is exhaustive.

Reusable technical functionality belongs in `Akay.To.*`.

Business rules, domain behavior and application-specific use cases belong in `Akay.Be`.

### Architecture Knowledge

The authoritative architectural documentation for `Akay.To.*` is the OKF knowledge bundle located at:

```text
../Akay.To/knowledge/architecture/index.md
```

When `../Akay.To` is available locally, use this documentation before inspecting implementation details.

The root `index.md` is the entry point for discovering available building blocks.

Do not load every architecture document by default.

Use progressive disclosure:

```text
architecture/index.md
        ↓
relevant feature document
        ↓
related documents when required
        ↓
Akay.To source code only when clarification is needed
```

### Mandatory Akay.To Discovery

Before implementing anything related to:

* persistence;
* EF Core;
* repositories;
* transactions;
* soft delete;
* auditing;
* outbox;
* domain events;
* integration events;
* messaging;
* dispatcher/mediator behavior;
* validation pipelines;
* caching pipelines;
* Azure services;
* Table Storage;
* Blob Storage;
* SignalR;
* AI infrastructure;
* embeddings;
* RAG;
* cross-cutting concerns;

first inspect:

```text
../Akay.To/knowledge/architecture/index.md
```

This list is intentionally non-exhaustive. Use the index to discover the capabilities currently provided by `Akay.To.*`.

### Using Akay.To Knowledge

For a potentially relevant Akay.To feature:

1. Read `knowledge/architecture/index.md`.
2. Identify the relevant feature document.
3. Read only that document initially.
4. Follow `Related` links only when required.
5. Pay particular attention to:

   * `Purpose`
   * `Provided by`
   * `Registration`
   * `Usage`
   * `Behavior`
   * `Conventions`
   * `Do not`
   * `Important considerations`
   * `Source references`
6. Use the documented Akay.To API and conventions.
7. Inspect Akay.To source code only when the documentation does not resolve an implementation detail.

### Do Not Reinvent Akay.To

Do not reimplement functionality already provided by `Akay.To.*`.

When Akay.To already provides a capability:

* do not create a local alternative in `Akay.Be`;
* do not duplicate its implementation;
* do not introduce another library solving the same concern without explicit justification;
* do not create parallel abstractions merely to hide Akay.To abstractions;
* do not bypass an existing building block because implementing a local version appears simpler.

In particular, respect the `Do not` section of each architecture document.

If an Akay.To building block cannot satisfy a requirement, identify the concrete limitation before implementing or proposing an alternative.

### Source of Truth

When determining how an Akay.To capability works, use this priority:

1. Akay.To OKF architecture documentation.
2. Current Akay.To source code when verification or additional detail is required.
3. Existing usage in Akay.Be.
4. External documentation for the underlying library or service when version-specific behavior must be verified.

Do not infer behavior solely from class, interface, namespace or package names.

Do not assume an existing usage in Akay.Be is correct when it conflicts with current Akay.To documentation.

## Application

Application contains use cases and orchestration specific to Akay.Be.

* Organize code by feature/use case.
* Commands and queries are dispatched through the Akay.To dispatcher/mediator.
* Pipeline concerns such as validation and caching should use the corresponding Akay.To mechanisms when available.
* Mapping between domain entities and API/application responses belongs here.
* Application may enforce use-case-specific rules but should not contain infrastructure implementations.
* Keep handlers focused on orchestration rather than infrastructure mechanics.

## Infrastructure

Infrastructure implements technical details required by Akay.Be.

* EF Core configuration and persistence implementations belong here.
* Azure integrations belong here.
* Provider-specific implementations belong here.
* Prefer Akay.To building blocks and extension points over custom infrastructure.
* Infrastructure may reference Application and Domain.
* Infrastructure-specific behavior must not leak into Domain.

## Rules

* Use async APIs and propagate `CancellationToken`.
* Use `Result` for expected outcomes.
* Use structured logging.
* Prefer existing Akay.To abstractions and extension methods.
* Do not add dependencies without justification.
* Do not add a library when the same concern is already solved by Akay.To or the .NET platform.
* Do not add EF Core migrations: the project deliberately has no migration workflow. Never propose generating a baseline migration; schema alignment is done with explicit, reviewed DDL scripts against the target database.
* Do not publish packages unless explicitly requested.
* Do not perform destructive changes unless explicitly requested.
* Do not weaken architecture boundaries to simplify a local implementation.

## Testing

Add:

* Unit tests for Domain and Application behavior.
* Integration tests for EF Core and repositories.
* Architecture tests for dependency rules.

Integration tests must use minimal controlled seed data against a schema created from the current model (no migrations).

When behavior depends on an Akay.To building block, test Akay.Be's integration with that building block rather than reproducing tests for Akay.To internals.

## Workflow

For non-trivial changes:

1. Inspect the existing feature and relevant domain model.
2. Determine whether the task involves infrastructure or a cross-cutting concern.
3. If it does, inspect `../Akay.To/knowledge/architecture/index.md`.
4. Read the relevant Akay.To feature documentation using progressive disclosure.
5. Verify existing Akay.Be usage when relevant.
6. Identify domain rules, architectural constraints and assumptions.
7. Produce a concise implementation plan.
8. Implement using existing Akay.To building blocks where applicable.
9. Add or update tests.
10. Build and run the relevant test suite.
11. Verify that no Akay.To functionality has been duplicated locally.

For small, isolated changes, avoid unnecessary ceremony while still respecting architecture and Akay.To reuse rules.

## External Documentation

Use Context7 only when current or version-specific external documentation is required.

Prefer project knowledge and source code for Akay.To behavior.

External documentation must not override project-specific conventions documented by Akay.To unless the project implementation is demonstrably outdated or incorrect.
