using System.Net;
using System.Reflection;
using AppEnclave;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Master services (shared across all enclaves)

var httpContextAccessor = new HttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);

builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.ConfigureHttpClientDefaults(b =>
    b.ConfigureHttpClient(client =>
    {
        client.DefaultRequestVersion = HttpVersion.Version20;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler()));

builder.Services.AddHttpClient();

await builder.Services.AddAppEnclaveAsync(options =>
{
    options.UseAuthentication = true;
    options.AllowSubAppsOnSameHost = true;
    options.Hosts = new[] { "localhost" };
    options.Plugin = new AppEnclave.Examples.ChildApp.EnclavePlugin();
    options.Name = "AppEnclave.Examples.ChildApp";
    options.EnvironmentName = "Host";
    options.ContentRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
    options.BinRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

await builder.Services.AddAppEnclaveAsync(options =>
{
    options.UseAuthentication = true;
    options.Path = "/subapp1";
    options.Hosts = new[] { "localhost" };
    options.Plugin = new AppEnclave.Examples.ChildApp.EnclavePlugin();
    options.Name = "AppEnclave.Examples.ChildApp";
    options.EnvironmentName = "SubApp1";
    options.ContentRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
    options.BinRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

await builder.Services.AddAppEnclaveAsync(options =>
{
    options.UseAuthentication = true;
    options.Path = "/subapp2";
    options.Hosts = new[] { "localhost" };
    options.Plugin = new AppEnclave.Examples.ChildApp.EnclavePlugin();
    options.Name = "AppEnclave.Examples.ChildApp";
    options.EnvironmentName = "SubApp2";
    options.ContentRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
    options.BinRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

var serviceName = "AppEnclave.Examples.MasterApp";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddSource(AppEnclaveMetrics.ActivitySource.Name)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                var routeData = response?.HttpContext?.GetRouteData();

                if (routeData?.Values?.TryGetValue("controller", out var controller) == true &&
                    routeData?.Values?.TryGetValue("action", out var action) == true
                    && !string.IsNullOrWhiteSpace(controller as string)
                    && !string.IsNullOrWhiteSpace(action as string))
                {
                    activity.DisplayName = $"{response?.HttpContext?.Request?.Method} {controller}/{action}";
                    activity.SetTag("controller", controller);
                    activity.SetTag("action", action);
                    activity.SetTag("http.route", activity.DisplayName);
                }
            };
        })
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(AppEnclaveMetrics.Meter.Name)
        .AddView(
            instrumentName: "http.server.request.duration",
            new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0 }
            })
        .AddConsoleExporter());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAppEnclave();

app.Run();
