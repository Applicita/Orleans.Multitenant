using Orleans.Multitenant;
using Orleans4Multitenant.Contracts.TenantContract;

namespace Orleans4Multitenant.Apis;

public abstract partial class ControllerBase(IClusterClient orleans) : Microsoft.AspNetCore.Mvc.ControllerBase
{
    protected ITenant RequestTenant
    {
        get
        {
            string tenantId = HttpContext.Request.Headers.TryGetValue(TenantHeader.Name, out var tenantHeaderValue) ? tenantHeaderValue.ToString() : string.Empty;
            return orleans.ForTenant(tenantId).GetGrain<ITenant>(ITenant.Id);
        }
    }
}
