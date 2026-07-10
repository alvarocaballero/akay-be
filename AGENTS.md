# AGENTS.md - Akay.Be

`Akay.Be` currently validates the `Akay.To.*` packages and is evolving into the production API for the Akay educational platform. Do not treat it as a disposable test project.

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
* Controllers and consumers remain thin and delegate through the mediator.
* Organize Application by feature or use case.
* Repositories return domain entities.
* Mapping to responses belongs in Application.
* EF Core, repositories and migrations belong in Infrastructure.
* Domain must not depend on EF Core, HTTP, Azure or AI providers.

## Domain

* Aggregate roots inherit from `AggregateRoot<int>`.
* Child entities inherit from `Entity<int>`.
* Use domain methods and avoid public setters.
* Protect invariants in Domain or Application.
* Educational resources and exercises require `TopicId`.
* `SectionId` is optional and, when present, must belong to the same topic.

## Akay.To

Packages:

* `Akay.To.Core`
* `Akay.To.EF`
* `Akay.To.Azure`
* `Akay.To.AI`

Before using a package feature, consult its `docs/` directory.

Do not reimplement functionality already provided by `Akay.To.*`.

Reusable technical functionality belongs in `Akay.To.*`. Business logic belongs in `Akay.Be`.

## Rules

* Use async APIs and propagate `CancellationToken`.
* Use `Result` for expected outcomes.
* Use structured logging.
* Do not add dependencies without justification.
* Do not create migrations, publish packages or perform destructive changes unless explicitly requested.

## Testing

Add:

* Unit tests for Domain and Application behavior.
* Integration tests for EF Core and repositories.
* Architecture tests for dependency rules.

Integration tests must use migrations and minimal controlled seed data.

## Workflow

For larger changes:

1. Inspect the existing feature and domain model.
2. Consult the relevant `Akay.To.*` documentation.
3. Identify assumptions and domain rules.
4. Produce a concise plan.
5. Implement, test and build.

Use Context7 only when current or version-specific documentation is required.
