using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace AppEnclave;

public class TenantEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = default;
    public string EnvironmentName { get; set; } = "Production";
    public string ContentRootPath { get; set; } = default;
    public IFileProvider ContentRootFileProvider { get; set; } = default;
    public string WebRootPath { get; set; } = default;
    public IFileProvider WebRootFileProvider { get; set; } = default;
}