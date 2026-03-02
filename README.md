[![License: MIT](https://img.shields.io/badge/licence-mit-blue)](https://opensource.org/license/mit) [![License: MIT](https://img.shields.io/badge/nuget-v1.0-brightgreen)](https://www.nuget.org/packages/AppEnclave)


# AppEnclave 🛡️

**A High-Performance Multi-tenant Application Micro-kernel for ASP.NET Core.**

AppEnclave allows you to host multiple, fully independent ASP.NET Core "Child Apps" within a single "Master App" process, sharing the same port. Each child app operates within its own **Enclave**—possessing its own private Dependency Injection container (`IServiceProvider`), Middleware pipeline, and Configuration.

---

## 🚀 Key Features

*   **Multi-tenant Micro-kernel:** Run multiple instances of the same app or different modules side-by-side.
*   **Total DI Isolation:** Each enclave has its own private `IServiceProvider`. No service registration leaks.
*   **Environment Injection:** Each tenant gets its own `IWebHostEnvironment` injected into its DI during the build process, allowing for isolated `ContentRoot` and `EnvironmentName`.
*   **Zero-Latency Internal Routing:** Requests are routed in-memory directly to the child's pipeline. No network overhead, no sockets, just raw performance.
*   **Low Memory Footprint:** Designed without **Assembly Load Contexts (ALC)** to maximize JIT sharing and minimize RAM usage.

---

## 🏗️ Architecture & Constraints

AppEnclave is built for efficiency. To achieve the lowest possible memory overhead, it uses a shared-binary approach:


| Feature | AppEnclave Approach | Benefit |
| :--- | :--- | :--- |
| **Process** | Single Process | Extremely low overhead, easy monitoring. |
| **Assembly Loading** | No ALC (Single Context) | **Low Memory:** Shared JIT code and type metadata. |
| **Dependencies** | Shared Versions | All apps must use the same library versions. |
| **Isolation** | Logical & DI Container | Complete separation of app logic and middleware. |
| **Performance** | In-Process | Faster than YARP or Nginx reverse proxying. |

> [!NOTE]  
> Because it does not use ALCs, the Master App and all Child Apps must target the same versions of shared libraries (e.g., `EntityFramework`, `Newtonsoft.Json`).

---

## 📦 Installation

You can install **AppEnclave** via NuGet using the .NET CLI or the Package Manager Console.

### .NET CLI (Recommended)
```bash
dotnet add package AppEnclave --version 1.0.0
```

## 💻 Quick Start
### 1. Create an Enclave plugin in Child app
```csharp
using Microsoft.Extensions.FileProviders;

namespace AppEnclave.Examples.ChildApp
{
    public class EnclavePlugin : ITenantPlugin
    {
        public Task ConfigureServicesAsync(IServiceCollection services, IWebHostEnvironment environment,
            IConfigurationManager configuration)
        {
            // Add services to the container.
            services.AddControllersWithViews();

            services.AddAuthentication();
            services.AddAuthorization();

            return Task.CompletedTask;
        }

        public Task ConfigureAsync(IApplicationBuilder app, IWebHostEnvironment environment)
        {
            // Configure the HTTP request pipeline.
            if (!environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseStaticFiles();

            if (!Directory.Exists(Path.Combine(environment.ContentRootPath, @"wwwroot/.well-known")))
            {
                Directory.CreateDirectory(Path.Combine(environment.ContentRootPath, @"wwwroot/.well-known"));
            }

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider =
                    new PhysicalFileProvider(Path.Combine(environment.ContentRootPath, @"wwwroot/.well-known")),
                RequestPath = new PathString("/.well-known"),
                ServeUnknownFileTypes = true
            });

            app.UseEndpoints(endpoints =>
            {               
                endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}")
                    .WithStaticAssets();
            });

            return Task.CompletedTask;
        }
    }
}
```

### You can even use that in Program.cs to keep the option to run the child app by itself 
```csharp
using AppEnclave.Examples.ChildApp;

var builder = WebApplication.CreateBuilder(args);

var plugin = new EnclavePlugin();

await plugin.ConfigureServicesAsync(builder.Services, builder.Environment, builder.Configuration);

var app = builder.Build();

await plugin.ConfigureAsync(app, builder.Environment);

app.Run();
```

### 2. Register an Enclave in Master app Program.cs
You can map an enclave to a specific path or entire hostname. AppEnclave handles the creation of the isolated container.

```csharp
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

// Child app for entire host localhost yet allowing subapps on same host

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

// Child app for /subapp1 path

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

// Child app for /subapp2 path

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

// Turns on routing middleware to enable enclaves to work

app.UseAppEnclave();

app.Run();
```

## 📂 Examples

*   **[Simple Web app able to run by itself or as an enclave in master app](./AppEnclave.Examples.ChildApp)** - Simple webapp able to run by itself or as an enclave in master app.
*   **[Basic Modular Monolith](./AppEnclave.Examples.MasterApp)** - Simple setup for isolated apps with separate configurations and shared root services.


## 🛠 Why use AppEnclave?

AppEnclave fills the gap between a messy monolithic pipeline and the heavy resource overhead of Microservices. It is the ideal choice for **Modular Monoliths** and **SaaS Multi-tenant** systems where performance and isolation are both critical.


| Feature | Standard `app.Map()` | Containers / Sidecars | **AppEnclave** |
| :--- | :---: | :---: | :---: |
| **DI Container Isolation** | ❌ Shared (Leaky) | ✅ Total | ✅ **Total (Private)** |
| **Memory Footprint** | 🟢 Lowest | 🔴 High (Multiple Runtimes) | 🟢 **Minimal (Shared JIT)** |
| **Configuration Isolation**| ❌ No | ✅ Yes | ✅ **Yes (Per-Enclave)** |
| **Custom `IWebHostEnv`** | ❌ No (Global) | ✅ Yes | ✅ **Yes (Isolated)** |
| **Port Sharing** | ✅ Yes | ❌ No (Requires Proxy) | ✅ **Yes (Native)** |
| **Network Latency** | Zero | 🔴 High (TCP/Socket) | 🟢 **Zero (In-Process)** |
| **Dependency Versioning**| ✅ Forced Same | ✅ Independent | ⚠️ **Forced Same (No ALC)** |

### When to choose AppEnclave?
*   **Modular Monoliths:** You want to keep the code in one repo and process, but you hate when Service A accidentally resolves a dependency meant for Service B.
*   **SaaS Multi-tenancy:** You need to run hundreds of instances of the same app with different `appsettings.json` and different `WebRootPath` without paying for massive RAM usage.
*   **Legacy Migrations:** You are merging multiple separate ASP.NET Core apps into one unified host without rewriting their internal DI registrations.
