using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AppEnclave;

public class TenantRegistry : ITenantRegistry
{
    public TenantRegistry()
    {
    }

    private readonly Dictionary<string, TenantInstance> _tenants = new();

    public void Register(string id, RequestDelegate entryPoint, IServiceProvider provider, bool useAuthentication, bool allowSubAppsOnSameHost) =>
        _tenants[id] = new TenantInstance() { EntryPoint = entryPoint, Provider = provider, UseAuthentication = useAuthentication, AllowSubAppsOnSameHost = allowSubAppsOnSameHost };
    
    public TenantInstanceInfo? GetTenantByPathOrHostName(HttpRequest request)
    {
        var tenant = _tenants.GetValueOrDefault(request.Host.Host);
        if (tenant != null && !tenant.AllowSubAppsOnSameHost)
        {
            return new TenantInstanceInfo() { Instance = tenant, Key = string.Empty };
        }

        var key = _tenants.Keys.FirstOrDefault(k => k.Contains("/") && request.Path.StartsWithSegments(k, StringComparison.OrdinalIgnoreCase));
        if (key == null && tenant != null)
        {
            return new TenantInstanceInfo() { Instance = tenant, Key = string.Empty };
        }

        return key != null ? new TenantInstanceInfo() { Instance = _tenants[key], Key = key } : null;
    }

    public IEnumerable<TenantInstance> GetTenants()
    {
        return _tenants.Values;
    }

    protected virtual async Task<RequestDelegateInfo> CreateRequestDelegateInfoAsync(
        IServiceCollection rootServices, 
        ITenantPlugin plugin,
        string name, 
        string environmentName, 
        string contentRootPath, 
        string binDirectory)
    {
        var services = new ServiceCollection();

        foreach (var descriptor in rootServices)
        {
            if (descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(TenantsHostedService))
            {
                continue;
            }

            services.Add(descriptor);
        }

        var environment = new TenantEnvironment
        {
            ApplicationName = name,
            ContentRootPath = contentRootPath,
            EnvironmentName = environmentName,
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath),
            WebRootPath = Path.Combine(contentRootPath, "wwwroot"),
            WebRootFileProvider = new PhysicalFileProvider(Path.Combine(contentRootPath, "wwwroot"))
        };

        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(binDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(binDirectory, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<IWebHostEnvironment>(environment);

        services
            .AddDataProtection()
            .SetApplicationName($"Tenant_{name}_{environmentName}");

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = $"Antiforgery_{name}_{environmentName}";
        });

        services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = 443;
        });

        await plugin.ConfigureServicesAsync(services, environment, configuration);

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        await plugin.ConfigureAsync(builder, environment);

        builder.Run(async context =>
        {
            var endpoint = context.GetEndpoint();
            if (endpoint != null && endpoint.RequestDelegate != null)
            {
                await endpoint.RequestDelegate(context);
            }
        });

        return new RequestDelegateInfo()
        {
            Pipeline = builder.Build(),
            Provider = builder.ApplicationServices
        };
    }

    public virtual async Task RegisterTenantByHostnameAsync(IServiceCollection rootServices,
        IEnumerable<string> hostnames, ITenantPlugin plugin,
        string name, string environmentName, string contentRootPath, string binDirectory, bool useAuthentication, bool allowSubAppsOnSameHost)    
    {
        var hostInfo = await CreateRequestDelegateInfoAsync(rootServices, plugin, name, environmentName, contentRootPath, binDirectory);

        foreach (var hostname in hostnames)
        {
            Register(hostname, hostInfo.Pipeline, hostInfo.Provider, useAuthentication, allowSubAppsOnSameHost);
        }
    }

    public virtual async Task RegisterTenantByPathAsync(IServiceCollection rootServices,
        string path, ITenantPlugin plugin,
        string name, string environmentName, string contentRootPath, string binDirectory, bool useAuthentication, bool allowSubAppsOnSameHost)
    {
        var hostInfo = await CreateRequestDelegateInfoAsync(rootServices, plugin, name, environmentName, contentRootPath, binDirectory);

        Register(path, hostInfo.Pipeline, hostInfo.Provider, useAuthentication, allowSubAppsOnSameHost);
    }
}