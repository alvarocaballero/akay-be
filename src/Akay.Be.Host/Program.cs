using Akay.Be.Host;

const string appConfigEndpointKey = "APP_CONFIGURATION_ENDPOINT"; // URL del azure App configuration, se puede configurar por variables de entorno o por appsettings.json
const string appConfigPrefixKey = "APP_CONFIGURATION_PREFIX";     // Prefijos de configuración a cargar desde Azure App Configuration, separados por ';', se pueden configurar por variables de entorno o por appsettings.json
const string keyVaultEndpointKey = "KEY_VAULT_ENDPOINT";          // URL del azure Key Vault, se puede configurar por variables de entorno o por appsettings.json


var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices(appConfigEndpointKey: appConfigEndpointKey,
                          appConfigPrefixKey: appConfigPrefixKey,
                          keyVaultEndpointKey: keyVaultEndpointKey);

var app = builder.Build();

app.Configure();

await app.RunAsync();
