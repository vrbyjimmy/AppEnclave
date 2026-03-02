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
