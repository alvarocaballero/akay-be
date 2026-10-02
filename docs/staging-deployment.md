# Deploy the Azure development environment as Staging

The Azure development Container App uses `ASPNETCORE_ENVIRONMENT=Staging`. Local execution uses `Development` and User Secrets. Azure credential selection is independent of the environment name and defaults to managed identity.

## Release sequence

1. Make `Akay.To.Azure.Identity` **2.0.0** available in the package feed first, then `Akay.To.Azure` **3.0.0**, `Akay.To.Messaging.Rebus` **2.0.0**, and `Akay.To.EF.Npgsql` **3.0.0**. Package publication is a separate release operation.
2. Build the Akay.Be image using the updated package versions. Include `appsettings.Staging.json` in the published output.
3. Create a new revision of `ca-akayadmin-be-dev-sp-002` with **both the new image and `ASPNETCORE_ENVIRONMENT=Staging`**. Set `DOTNET_ENVIRONMENT=Staging` too if it is currently defined; do not leave conflicting environment names. Keep the existing user-assigned identity and its permissions.
4. Leave `AZURE_CREDENTIAL_MODE` unset or set it to `ManagedIdentity`. Remove any existing `Developer`/`ClientSecret` override. If `AZURE_CLIENT_ID` is set on the Container App, it must identify the assigned managed identity, not the Entra login application.
5. Check revision readiness, startup logs, and dependency health. Call `POST /api/auth/exchange` with an Entra access token for the API; a valid linked active user should receive an Akay token. Verify a normal Akay-authenticated endpoint as well.

Do not change the environment on the old image first: its credential factory excludes managed identity outside Production. For rollback, restore the previous image/revision together with its original environment settings.

## Configuration ownership

| Source | Purpose |
| --- | --- |
| `appsettings.json` | Shared application defaults |
| `appsettings.Development.json` | Local defaults, including explicit `Developer` credential mode |
| User Secrets | Local secrets and overrides, including explicit `ClientSecret` mode when needed |
| `appsettings.Staging.json` | Azure development endpoints, assigned identity IDs, and the development Entra tenant/application |
| Key Vault | Signing keys, API keys, connection strings and other secrets |
| `appsettings.Production.json` | Reserved for future production configuration; currently empty |

`SecuritySettings:EntraExternalId:ClientId` identifies the login/API application. `AZURE_CLIENT_ID` identifies the user-assigned identity used to access infrastructure. They are different identities. DomainName is configuration, not a secret.

The Staging file retains the existing Azure development resources and adds the Entra values. It deliberately does not load localhost CORS/Qdrant defaults. Configure hosted frontend CORS origins and any required Qdrant endpoint separately for the deployed environment.

## Local authentication

For CLI-based credentials, the Development file already selects `Developer`; sign in with the appropriate developer tool. For service-principal credentials, set these keys in the Host project's User Secrets:

- `AZURE_CREDENTIAL_MODE`: `ClientSecret`
- `AZURE_TENANT_ID`: the service-principal tenant
- `AZURE_CLIENT_ID`: the service-principal application ID
- `AZURE_CLIENT_SECRET`: the secret value

These values must be available before connecting to Key Vault. Storage, Service Bus, and PostgreSQL read the same configuration through the shared factory. Mode names are case-insensitive; unknown modes and incomplete client-secret settings fail without silently choosing another credential.

Akay.Be validates required Entra settings at startup, so missing authentication configuration produces a configuration error rather than a 500 on the first exchange request. Future Production deployments must supply their own endpoints and Entra settings before startup.

## Local verification

```powershell
dotnet test Akay.Be.slnx --configuration Release -p:UseLocalAkayTo=true
```

The Staging authentication contract test binds the shipped configuration and verifies registration of Entra and Akay schemes without accessing Azure. Successful token acquisition, deployed Key Vault access, and the real exchange flow still require validation on the new Azure revision.
