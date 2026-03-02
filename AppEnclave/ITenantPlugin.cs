using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppEnclave
{
    public interface ITenantPlugin
    {
        Task ConfigureServicesAsync(IServiceCollection services, IWebHostEnvironment environment, IConfigurationManager configuration);
        
        Task ConfigureAsync(IApplicationBuilder app, IWebHostEnvironment environment);
    }
}
