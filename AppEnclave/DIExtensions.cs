using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AppEnclave
{
    public static class DIExtensions
    {
        public static async Task<IServiceCollection> AddAppEnclaveAsync(this IServiceCollection services,
            Action<AppEnclaveOptions> configure)
        {
            services.TryAddSingleton<ITenantRegistry>(new TenantRegistry());

            var serviceDescriptor = services.First(s => s.ServiceType == typeof(ITenantRegistry));
            var registry = serviceDescriptor.ImplementationInstance as TenantRegistry;
            if (registry == null)
            {
                throw new InvalidOperationException("Failed to resolve TenantRegistry from the service collection.");
            }

            var options = new AppEnclaveOptions();
            configure(options);

            if (!string.IsNullOrWhiteSpace(options.Path))
            {
                await registry.RegisterTenantByPathAsync(services, options.Path, options.Plugin, options.Name,
                    options.EnvironmentName, options.ContentRoot, options.BinRoot, options.UseAuthentication, options.AllowSubAppsOnSameHost, options.Hosts).ConfigureAwait(false);
            }
            else if (options.Hosts?.Any(x => !string.IsNullOrWhiteSpace(x)) == true)
            {
                await registry.RegisterTenantByHostnameAsync(services, options.Hosts, options.Plugin, options.Name,
                    options.EnvironmentName, options.ContentRoot, options.BinRoot, options.UseAuthentication, options.AllowSubAppsOnSameHost).ConfigureAwait(false);
            }
            else
            {
                throw new Exception("Either Path or Hosts must be specified for tenant registration.");
            }

            if (!services.Any(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(TenantsHostedService)))
            {
                services.AddHostedService<TenantsHostedService>();
            }

            return services;
        }

        public static IApplicationBuilder UseAppEnclave(this IApplicationBuilder app)
        {
            app.UseMiddleware<TenantDispatcherMiddleware>();

            return app;
        }

        public static bool IsTenant(this IWebHostEnvironment env)
        {
            return env is TenantEnvironment;
        }
    }
}
