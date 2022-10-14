using Microsoft.AspNetCore.Mvc;
using Orleans;
using Orleans.Multitenant;
using Orleans4Multitenant.Contracts.Tenant;

namespace Orleans4Multitenant.Apis;

public abstract partial class Base : ControllerBase
{
    readonly IClusterClient orleans;

    protected ITenant Tenant
    {
        get
        {
            string tenantId = HttpContext.Request.Headers.TryGetValue(TenantHeader.Name, out var tenantHeaderValue) ? tenantHeaderValue.ToString() : string.Empty;
            return orleans.ForTenant(tenantId).GetGrain<ITenant>(ITenant.Id);
        }
    }

    public Base(IClusterClient orleans) => this.orleans = orleans;
}
