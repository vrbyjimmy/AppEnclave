using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AppEnclave;

public class TenantDispatcherMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantRegistry _registry;

    public TenantDispatcherMiddleware(RequestDelegate next, ITenantRegistry registry)
    {
        _next = next;
        _registry = registry;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantInfo = _registry.GetTenantByPathOrHostName(context.Request);
        var tenant = tenantInfo?.Instance;

        if (tenant != null && tenant.EntryPoint != null && tenant.Provider != null)
        {
            var childPipeline = tenant.EntryPoint;

            if (tenant.UseAuthentication)
            {
                var childAppBuilder = new ApplicationBuilder(tenant.Provider);

                childAppBuilder.UseAuthentication();
                childAppBuilder.UseAuthorization();

                childAppBuilder.Run(tenant.EntryPoint);

                childPipeline = childAppBuilder.Build();
            }

            using (var scope = tenant.Provider.CreateScope())
            {
                var originalServices = context.RequestServices;
                context.RequestServices = scope.ServiceProvider;

                try
                {
                    if (context.Request.HasFormContentType || context.Request.ContentLength > 0)
                    {
                        context.Request.EnableBuffering();
                        context.Request.Body.Position = 0;
                    }

                    context.SetEndpoint(null);

                    var routeData = context.GetRouteData();
                    if (routeData != null)
                    {
                        routeData.Values.Clear();
                        routeData.Routers.Clear();
                    }

                    var routeValuesFeature = new RouteValuesFeature();
                    routeValuesFeature.RouteValues["controller"] = "Home";
                    routeValuesFeature.RouteValues["action"] = "Index";

                    context.Features.Set<IRouteValuesFeature>(routeValuesFeature);
                    context.Features.Set<IEndpointFeature>(null);
                    context.Features.Set<IRoutingFeature>(null);

                    var originalPath = context.Request.Path;
                    var originalPathBase = context.Request.PathBase;
                    context.Request.PathBase = PathString.Empty;

                    if (!string.IsNullOrWhiteSpace(tenantInfo?.Key) 
                        && context.Request.Path.StartsWithSegments(tenantInfo?.Key, out var remainingPath))
                    {
                        context.Request.Path = remainingPath;
                    }

                    try
                    {
                        await childPipeline(context);
                    }
                    finally
                    {
                        context.Request.PathBase = originalPathBase;
                        context.Request.Path = originalPath;
                    }
                }
                finally
                {
                    context.RequestServices = originalServices;
                }
            }

            if (context.Response.StatusCode == StatusCodes.Status200OK 
                && !context.Response.HasStarted)
            {
                var endpoint = context.GetEndpoint();
                if (endpoint == null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                }
            }
        }
        else
        {
            await _next(context);
        }
    }
}