using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

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
        var masterDiagnosticListener = context.RequestServices.GetService<DiagnosticListener>();
        var isMasterOtelActive = masterDiagnosticListener != null && Activity.Current != null;
        
        if (tenant != null && tenant.EntryPoint != null && tenant.Provider != null)
        {
            AppEnclaveMetrics.ActiveRequestsCounter.Add(1);
            var sw = Stopwatch.StartNew();
            var controller = string.Empty;
            var action = string.Empty;

            var rdata = context.GetRouteData();
            controller = rdata.Values["controller"]?.ToString();
            action = rdata.Values["action"]?.ToString();

            var childDiagnosticListener = tenant.Provider.GetService<DiagnosticListener>();
            var shouldTriggerChild = !isMasterOtelActive && childDiagnosticListener != null && childDiagnosticListener.IsEnabled("Microsoft.AspNetCore");

            using var tenantActivity = AppEnclaveMetrics.ActivitySource.StartActivity("TenantRequest", ActivityKind.Server);

            tenantActivity?.SetTag("http.host", context.Request.Host.Value);
            tenantActivity?.SetTag("http.method", context.Request.Method);

            if (shouldTriggerChild)
            {
                childDiagnosticListener?.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context);
            }
            else if (isMasterOtelActive)
            {
                masterDiagnosticListener?.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context);
            }
            
            try
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
                            await childPipeline(context).ConfigureAwait(false);
                        }
                        finally
                        {
                            context.Request.PathBase = originalPathBase;
                            context.Request.Path = originalPath;
                        }

                        rdata = context.GetRouteData();
                        controller = rdata.Values["controller"]?.ToString();
                        action = rdata.Values["action"]?.ToString();

                        tenantActivity?.SetTag("http.status_code", context.Response.StatusCode);
                        tenantActivity?.SetTag("http.route", $"{controller}/{action}");
                    }
                    catch (Exception ex)
                    {
                        tenantActivity?.SetStatus(ActivityStatusCode.Error);
                        tenantActivity?.AddException(ex);
                        throw;
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
            finally
            {
                var metricsFeature = context.Features.Get<IHttpMetricsTagsFeature>();

                if (metricsFeature != null)
                {
                    if (!string.IsNullOrWhiteSpace(controller) && !string.IsNullOrWhiteSpace(action))
                    {
                        metricsFeature.Tags.Add(new KeyValuePair<string, object?>("http.route", $"{controller}/{action}"));
                        metricsFeature.Tags.Add(new KeyValuePair<string, object?>("http.host", context.Request.Host.Host));
                    }
                }

                double durationInSeconds = sw.Elapsed.TotalSeconds;

                AppEnclaveMetrics.RequestDuration.Record(durationInSeconds,
                    new KeyValuePair<string, object?>("http.request.method", context.Request.Method),
                    new KeyValuePair<string, object?>("http.response.status_code", context.Response.StatusCode),
                    new KeyValuePair<string, object?>("tenant.host", context.Request.Host.Value),
                    new KeyValuePair<string, object?>("network.protocol.version", context.Request.Protocol),
                    new KeyValuePair<string, object?>("http.route", $"{controller}/{action}"),
                    new KeyValuePair<string, object?>("url.path", context.Request.Path),
                    new KeyValuePair<string, object?>("url.scheme", context.Request.Scheme),
                    new KeyValuePair<string, object?>("server.address", context.Request.Host.Host)
                );

                AppEnclaveMetrics.RequestCounter.Add(1,
                    new KeyValuePair<string, object?>("http.request.method", context.Request.Method),
                    new KeyValuePair<string, object?>("http.response.status_code", context.Response.StatusCode),
                    new KeyValuePair<string, object?>("tenant.host", context.Request.Host.Value),
                    new KeyValuePair<string, object?>("network.protocol.version", context.Request.Protocol),
                    new KeyValuePair<string, object?>("http.route", $"{controller}/{action}"),
                    new KeyValuePair<string, object?>("url.path", context.Request.Path),
                    new KeyValuePair<string, object?>("url.scheme", context.Request.Scheme),
                    new KeyValuePair<string, object?>("server.address", context.Request.Host.Host)
                );

                AppEnclaveMetrics.ActiveRequestsCounter.Add(-1);

                if (shouldTriggerChild)
                {
                    childDiagnosticListener?.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context);
                }
                else if (isMasterOtelActive)
                {
                    masterDiagnosticListener?.Write("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context);

                    var activity = Activity.Current;
                    if (activity != null)
                    {
                        if (activity.Duration == TimeSpan.Zero)
                        {
                            activity.SetEndTime(DateTime.UtcNow);
                        }

                        activity.Stop();

                        Activity.Current = null;
                    }
                }
            }
        }
        else
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}