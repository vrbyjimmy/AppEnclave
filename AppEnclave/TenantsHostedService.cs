using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppEnclave;

public class TenantsHostedService : IHostedService
{
    private readonly ITenantRegistry _registry;
    private readonly List<IHostedService> _tenantServices = new();

    public TenantsHostedService(ITenantRegistry registry)
    {
        _registry = registry;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var tenant in _registry.GetTenants())
        {
            var tenantProvider = tenant.Provider;

            if (tenantProvider != null)
            {
                foreach (var hostedService in tenantProvider.GetServices<IHostedService>())
                {
                    if (hostedService is TenantsHostedService)
                    {
                        continue;
                    }

                    _tenantServices.Add(hostedService);
                    await hostedService.StartAsync(cancellationToken);
                }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var service in _tenantServices)
        {
            await service.StopAsync(cancellationToken);
        }
    }
}