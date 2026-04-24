# AGENTS.md - Akay.Be

## Build Commands

```powershell
# Local development (uses ProjectReference to Akay.To.* if ../Akay.To exists)
.\build-local.cmd

# CI / Pre-push (forces PackageReference only)
.\build-ci.cmd
```

## Private Packages

- GitHub Packages feed: `https://nuget.pkg.github.com/alvarocaballero`
- Packages: `Akay.To.*` (mapped in `NuGet.Config`)
- Credentials: env vars `GITHUB_PACKAGES_USERNAME`, `GITHUB_PACKAGES_TOKEN`
- CI uses secret `GH_PACKAGES_READ_TOKEN`

## Project Structure

- Solution: `Akay.Be.slnx` (not .sln)
- Source: `src/`
- Tests: `test/`
- Entry point: `src/Akay.Be.Host/Program.cs`

## Architecture

- Clean Architecture layers:
  - `Akay.Be.Host` → `Application`, `Infrastructure`
  - `Application` → `Domain`
  - `Infrastructure` → `Application`, `Domain`
- Private deps via `UseLocalAkayTo` property (see `Akay.Be.Host.csproj` conditions)

## Testing

- Run: `dotnet test Akay.Be.slnx --configuration Release`
- Architecture tests: `test/Akay.Be.Architecture.Tests/`

## CI

- Workflow: `.github/workflows/ci.yml`
- Branch protection: `main` requires PR + 1 approval
- Secrets: `GH_PACKAGES_READ_TOKEN` (GitHub repo secrets)