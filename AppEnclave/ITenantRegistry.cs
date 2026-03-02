using Microsoft.AspNetCore.Http;

namespace AppEnclave;

public interface ITenantRegistry
{
    TenantInstanceInfo? GetTenantByPathOrHostName(HttpRequest request);
    IEnumerable<TenantInstance> GetTenants();
}