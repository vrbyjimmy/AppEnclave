using AppEnclave.Examples.ChildApp;

var builder = WebApplication.CreateBuilder(args);

var plugin = new EnclavePlugin();

await plugin.ConfigureServicesAsync(builder.Services, builder.Environment, builder.Configuration);

var app = builder.Build();

await plugin.ConfigureAsync(app, builder.Environment);

app.Run();