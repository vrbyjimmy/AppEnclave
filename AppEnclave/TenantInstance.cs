using Microsoft.AspNetCore.Http;

namespace AppEnclave
{
    public class TenantInstance
    {
        public RequestDelegate? EntryPoint { get; set; }
        public IServiceProvider? Provider { get; set; }
        public bool UseAuthentication { get; set; }
        public bool AllowSubAppsOnSameHost { get; set; }
    }
}
