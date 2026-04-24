using Akay.Be.Host;

const string appConfigEndpointKey = "APP_CONFIGURATION__ENDPOINT"; // URL del azure App configuration, se puede configurar por variables de entorno o por appsettings.json
const string appConfigPrefixKey = "APP_CONFIGURATION__PREFIX";     // Prefijos de configuración a cargar desde Azure App Configuration, separados por ';', se pueden configurar por variables de entorno o por appsettings.json

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices(appConfigEndpointKey, appConfigPrefixKey);

var app = builder.Build();

app.Configure(app.Environment);

await app.RunAsync();