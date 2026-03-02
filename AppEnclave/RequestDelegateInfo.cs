using Microsoft.AspNetCore.Http;

namespace AppEnclave;

public class RequestDelegateInfo
{
    public RequestDelegate? Pipeline { get; set; }
    public IServiceProvider? Provider { get; set; }
}