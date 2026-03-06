using Microsoft.Extensions.FileProviders;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

            var configSection = configuration.GetSection("ChildOptions");
            services.Configure<ChildOptions>(configSection);

            // add open telemetry only for the master, not for the tenant, to avoid duplicate telemetry when master is also instrumented.
            if (!environment.IsTenant())
            {
                var serviceName = "AppEnclave.Examples.ChildApp";

                services.AddOpenTelemetry()
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
            }

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
