# Akay.Be

Commit: `1ecc874` - Integracion de Akay.To y configuracion modular de servicios.

- Se anaden referencias a `Akay.To.Core` y `Akay.To.Azure` para soporte de componentes compartidos.
- Se modulariza el registro/configuracion en `Application`, `Infrastructure` y `Host`.
- Se incorpora `ApplicationSettings` y su validador.
- Se ajusta el arranque para soporte de Azure App Configuration.
- Se actualiza el pipeline de CI para acceso a paquetes privados.

### Archivos principales afectados

- `.github/workflows/ci.yml`
- `Directory.Packages.props`
- `NuGet.Config`
- `src/Akay.Be.Application/ApplicationRegisterModule.cs`
- `src/Akay.Be.Application/ApplicationSettings.cs`
- `src/Akay.Be.Application/ApplicationSettingsValidator.cs`
- `src/Akay.Be.Host/HostRegisterModule.cs`
- `src/Akay.Be.Host/Program.cs`
- `src/Akay.Be.Infrastructure/InfrastructureRegisterModule.cs`

### Último commit publicado

- **Hash:** `10745ba`
- **Fecha:** 2026-04-24 10:35:57 +0200
- **Autor:** alvaro.caballero
- **Mensaje:** Soporte condicional para dependencias Akay.To y scripts build
- **Archivos afectados:**
  - `Akay.Be.slnx`
  - `builc-ci.cmd`
  - `build-ci.cmd`
  - `build-ci.ps1`
  - `build-local.cmd`
  - `build-local.ps1`
  - `src/Akay.Be.Application/Akay.Be.Application.csproj`
  - `src/Akay.Be.Host/Akay.Be.Host.csproj`
