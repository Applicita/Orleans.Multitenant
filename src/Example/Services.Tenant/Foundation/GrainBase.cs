using Orleans;
using Orleans.Multitenant;
using Orleans.Runtime;

namespace Orleans4Multitenant.Services;

abstract class GrainBase<T> : GrainBase
{
    protected readonly IPersistentState<T> state;
    protected T S => state.State;

    protected GrainBase(IPersistentState<T> state) => this.state = state;
}

abstract class GrainBase : Grain
{
    internal T GetGrain<T>(Guid id) where T : IGrainWithStringKey
     => this.GetTenantGrainFactory().GetGrain<T>(id.ToString());

    protected void ThrowIfNotEqualToKeyWithinTenant(Guid id)
    {
        string key = this.GetKeyWithinTenant();
        if (key != id.ToString())
            throw new InvalidOperationException($"The specified id {id} does not equal this grain's id {key}");
    }
}


