using Orleans;
using Orleans.Multitenant;
using Orleans4Multitenant.Contracts.TenantContract;

namespace Orleans4Multitenant.Apis;

public abstract partial class ControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
{
    readonly IClusterClient orleans;

    protected ITenant RequestTenant
    {
        get
        {
            string tenantId = HttpContext.Request.Headers.TryGetValue(TenantHeader.Name, out var tenantHeaderValue) ? tenantHeaderValue.ToString() : string.Empty;
            return orleans.ForTenant(tenantId).GetGrain<ITenant>(ITenant.Id);
        }
    }

    public ControllerBase(IClusterClient orleans) => this.orleans = orleans;
}
