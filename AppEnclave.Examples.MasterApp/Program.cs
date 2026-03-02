using System.Net;
using AppEnclave;

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
    options.BinRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

await builder.Services.AddAppEnclaveAsync(options =>
{
    options.UseAuthentication = true;
    options.Hosts = new[] { "/subapp1" };
    options.Plugin = new AppEnclave.Examples.ChildApp.EnclavePlugin();
    options.Name = "AppEnclave.Examples.ChildApp";
    options.EnvironmentName = "SubApp1";
    options.ContentRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
    options.BinRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

await builder.Services.AddAppEnclaveAsync(options =>
{
    options.UseAuthentication = true;
    options.Hosts = new[] { "/subapp2" };
    options.Plugin = new AppEnclave.Examples.ChildApp.EnclavePlugin();
    options.Name = "AppEnclave.Examples.ChildApp";
    options.EnvironmentName = "SubApp2";
    options.ContentRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
    options.BinRoot = builder.Environment.ContentRootPath.Replace("AppEnclave.Examples.MasterApp", "AppEnclave.Examples.ChildApp");
});

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
