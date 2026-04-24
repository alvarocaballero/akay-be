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
