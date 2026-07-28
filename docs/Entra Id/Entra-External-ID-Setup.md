# Configuracion de Microsoft Entra External ID

Guia de referencia para usar Microsoft Entra External ID con `Akay.Be` sin provisioning Graph en el flujo principal.

## Resumen

- `Akay.Be` crea, edita y borra usuarios solo en su base de datos.
- Los usuarios crean su cuenta en Entra desde el user flow correspondiente la primera vez que entran.
- `POST /api/auth/exchange` vincula la cuenta Entra al usuario local por email y devuelve el JWT de Akay.
- Las identidades Entra sin correspondencia local, o ya no validas, se encolan en la tabla de outbox para que un worker externo las elimine despues.

## 1. Crear el tenant de Entra External ID

1. Abre `https://entra.microsoft.com` en una ventana de incognito.
2. Inicia sesion con una cuenta con permisos para crear tenants.
3. Ve a **Entra ID** -> **Overview** -> **Manage tenants** -> **Create**.
4. Selecciona **External**.
5. Completa el asistente:
   - **Organization name**
   - **Domain name**
   - **Country/Region**
   - **Subscription** y **Resource group**
6. Espera a que termine la creacion.
7. Cambia manualmente al nuevo directorio.

## 2. Datos del tenant

Recoge estos valores en **Entra ID** -> **Overview**:

| Dato | Uso |
|------|-----|
| Tenant ID | `SecuritySettings:EntraExternalId:TenantId` |
| Primary domain | informacion del tenant |
| Tenant login | `SecuritySettings:EntraExternalId:Instance` |

`Instance` debe quedar asi:

```text
https://{tenant-login}.ciamlogin.com/
```

## 3. Usuario administrador interno del tenant

1. Ve a **Users** -> **New user** -> **Create new user**.
2. Crea un usuario interno, por ejemplo `admin@tu-tenant.onmicrosoft.com`.
3. Asignale `Global Administrator`.

Usalo para administrar app registrations, user flows y consentimientos.

## 4. Registrar la aplicacion de la API

1. Ve a **App registrations** -> **New registration**.
2. Completa:
   - **Name**: el que hayais decidido para la API
   - **Supported account types**: **Accounts in this organizational directory only**
3. Pulsa **Register**.
4. En `Overview`, copia el **Application (client) ID**.
5. En **Expose an API**:
   - configura `Application ID URI` como `api://{api-client-id}`
   - crea el scope `access_as_user`

Este `client id` se usa para:

- `SecuritySettings:EntraExternalId:ClientId`
- `SecuritySettings:EntraExternalId:Audience`
- scope `api://{api-client-id}/access_as_user`

### 4.1. Client secret de la API

Solo hace falta si vas a usar la app de la API como identidad tecnica para:

- Azure App Configuration
- Azure Key Vault
- un worker externo que use `Akay.To.Azure` para operar contra Graph

Pasos:

1. Ve a **Certificates & secrets**.
2. Pulsa **New client secret**.
3. Copia el campo **Value**.

Ese valor se guardara como `AZURE_CLIENT_SECRET`.

## 5. Registrar la app cliente de Postman

1. Ve a **App registrations** -> **New registration**.
2. Completa:
   - **Name**: `Akay Admin Postman Dev`
   - **Supported account types**: **Accounts in this organizational directory only**
3. Pulsa **Register**.
4. En `Overview`, copia el **Application (client) ID**. Este sera `entraClientId`.
5. Ve a **Authentication**:
   - **Add a platform** -> **Mobile and desktop applications**
   - Redirect URI: `https://oauth.pstmn.io/v1/callback`
   - **Allow public client flows** -> **Yes**
6. Ve a **API permissions**:
   - **Add a permission**
   - **My APIs** o **APIs my organization uses**
   - selecciona la app de la API
   - marca `access_as_user`
   - pulsa **Add permissions**
7. Pulsa **Grant admin consent**.
8. Opcional: en **Token configuration** añade el claim `email`.

## 6. Crear los user flows

Entra CIAM usa un metodo por flow. Para el modelo actual necesitas dos:

| Usuario | Metodo | Flow |
|---------|--------|------|
| Admin / Teacher | email + password | `B2C_1_signin_password` |
| Student | email + OTP | `B2C_1_signin_otp` |

### 6.1. Flow de admin y teacher

1. Ve a **External Identities** -> **User flows**.
2. Pulsa **New user flow**.
3. Selecciona **Sign up and sign in**.
4. Configura:
   - **Name**: `B2C_1_signin_password`
   - **Identity providers**: email con password
   - **User attributes and token claims**: `Email`, `Given Name`, `Surname`, `Display Name`
5. Pulsa **Create**.

### 6.2. Flow de student

1. Ve a **External Identities** -> **User flows**.
2. Pulsa **New user flow**.
3. Selecciona **Sign up and sign in**.
4. Configura:
   - **Name**: `B2C_1_signin_otp`
   - **Identity providers**: email one-time passcode
   - **User attributes and token claims**: `Email`, `Given Name`, `Surname`, `Display Name`
5. Pulsa **Create**.

### 6.3. Asociar apps cliente a los flows

Cada app cliente se asocia al flow que corresponda en:

**User flows** -> selecciona el flow -> **Applications** -> **Add application**

Para Postman, si solo mantienes la coleccion de password, asociala a `B2C_1_signin_password`.

## 7. Modelo de usuarios en Akay

Regla principal:

- el usuario debe existir antes en la base de datos de Akay
- no se crea automaticamente en Akay desde Entra

### 7.1. Alta local

Un admin crea el usuario solo en Akay mediante `POST /api/users`.

Se guarda:

- email
- nombre
- apellidos
- roles
- `ExternalId = null`

### 7.2. Primer acceso

El usuario entra por su frontend:

- admin / teacher -> flow password
- student -> flow OTP

Si en Entra aun no tiene cuenta, debe pulsar **"¿No tiene una cuenta? Cree una"**.

Entra pedira:

- email
- given name
- last name
- display name
- password u OTP segun el flow

Despues, el frontend llama a `POST /api/auth/exchange`.

### 7.3. Que hace `exchange`

1. Lee el `ExternalId` y el `email` del token Entra.
2. Busca un usuario local por `ExternalId`.
3. Si no existe, busca por email.
4. Si encuentra un usuario local:
   - vincula `ExternalId`
   - devuelve el JWT de Akay
5. Si no encuentra usuario local:
   - rechaza el acceso con `auth.exchange.user_not_found`
   - encola la identidad externa para limpieza posterior

## 8. Configuracion funcional de la API

```json
"SecuritySettings": {
  "AuthenticationType": "BearerOrApiKey",
  "Jwt": {
    "Audience": "akay.be",
    "Issuer": "akay.bff",
    "SigningKey": "una-clave-super-segura-de-al-menos-32-caracteres",
    "ExpirationMinutes": 60
  },
  "EntraExternalId": {
    "Instance": "https://{tenant-login}.ciamlogin.com/",
    "TenantId": "{tenant-id}",
    "ClientId": "{api-client-id}",
    "Audience": "{api-client-id}"
  }
}
```

Importante:

- `SecuritySettings:EntraExternalId:ClientId` debe ser el **client id de la API**
- `SecuritySettings:EntraExternalId:Audience` debe ser el **client id de la API**
- no pongas aqui el client id de Postman

## 9. Credenciales Azure en local

Solo hacen falta si usas:

- Azure App Configuration
- Azure Key Vault

Guarda estas claves en User Secrets del host:

```powershell
dotnet user-secrets set "AZURE_TENANT_ID" "tu-tenant-id" --project .\src\TuApp.Host\TuApp.Host.csproj
dotnet user-secrets set "AZURE_CLIENT_ID" "tu-client-id" --project .\src\TuApp.Host\TuApp.Host.csproj
dotnet user-secrets set "AZURE_CLIENT_SECRET" "tu-client-secret" --project .\src\TuApp.Host\TuApp.Host.csproj
```

## 10. Configurar Postman

### 10.1. Variables de entorno

| Variable | Valor |
|----------|-------|
| `baseUrl` | URL de la API |
| `entraTenantName` | host CIAM |
| `entraTenantId` | GUID del tenant |
| `entraClientId` | client id de la app de Postman |
| `entraApiClientId` | client id de la API |
| `token` | vacia |

### 10.2. OAuth 2.0

| Campo | Valor |
|-------|-------|
| Grant Type | `Authorization Code (With PKCE)` |
| Auth URL | `https://{{entraTenantName}}.ciamlogin.com/{{entraTenantId}}/oauth2/v2.0/authorize` |
| Access Token URL | `https://{{entraTenantName}}.ciamlogin.com/{{entraTenantId}}/oauth2/v2.0/token` |
| Client ID | `{{entraClientId}}` |
| Client Secret | vacio |
| Scope | `openid profile email api://{{entraApiClientId}}/access_as_user` |
| Callback URL | `https://oauth.pstmn.io/v1/callback` |
| Code Challenge Method | `S256` |
| State | `{{$randomUUID}}` |
| Client Authentication | `Send as Request Body` |

## 11. Limpieza de identidades externas

`Akay.Be` no borra usuarios en Entra en tiempo real.

En su lugar, escribe solicitudes de limpieza en la tabla de outbox ya existente: `infra.__OutboxMessages`.

Se encola una solicitud cuando:

- un usuario local se borra y tenia `ExternalId`
- un usuario cambia de email y tenia `ExternalId`
- `exchange` recibe un token Entra que no corresponde a ningun usuario local

Un worker externo debe leer esos mensajes y eliminar la cuenta Entra usando `Akay.To.Azure`.

## 12. Errores comunes

| Error | Causa | Solucion |
|-------|-------|----------|
| `AADSTS500208` | Se usa `login.microsoftonline.com` en lugar de `ciamlogin.com`. | Usa `https://{tenant}.ciamlogin.com`. |
| `AADSTS900144` | El token request no envia bien el `client_id`. | En Postman usa `Client Authentication = Send as Request Body`. |
| `AADSTS7000218` | La app cliente de Postman esta configurada como confidential client y no se envio secret. | Usa `Mobile and desktop applications` + `Allow public client flows = Yes`. |
| `auth.exchange.user_not_found` | El email del token Entra no existe en Akay. | Crea primero el usuario en la base de datos local. La identidad Entra se encolara para limpieza. |
| `auth.exchange.user_inactive` | El usuario existe en Akay pero esta inactivo o borrado. | Activa el usuario o revisa el borrado. |
| `DefaultAzureCredential failed to retrieve a token` | No hay credenciales Azure validas en local y usas App Configuration o Key Vault. | Configura `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` y `AZURE_CLIENT_SECRET`. |

## 13. Resumen de datos a recoger

| Dato | Uso |
|------|-----|
| Tenant ID | `EntraExternalId.TenantId` |
| Tenant login | `EntraExternalId.Instance` |
| API client ID | `EntraExternalId.ClientId` y `EntraExternalId.Audience` |
| API scope | `api://{api-client-id}/access_as_user` |
| Postman client ID | `entraClientId` |
| JWT signing key | `Jwt.SigningKey` |

## 14. Que queda fuera de Akay.Be

La gestion activa de usuarios en Entra mediante Graph queda fuera de `Akay.Be`.

Si necesitas:

- crear usuarios Entra por backend
- modificar perfiles Entra
- desactivar usuarios Entra
- borrar usuarios Entra

usa `Akay.To.Azure` y su documentacion especifica de provisioning.
