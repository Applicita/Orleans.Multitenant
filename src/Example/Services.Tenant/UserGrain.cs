using Orleans;
using Orleans.Runtime;

namespace Orleans4Multitenant.Services.TenantService;

// All public grain contracts for all services must be in the Contracts assembly, using a separate subnamespace for each service

// All grain contracts defined in a service assembly must be internal

// All interfaces and types defined in a service assembly must be non-public;
// the public keyword is *only* used on interface member implementations, grain constructors and serializable members in a type.
// This ensures that the only external code access is Orleans instantiating grains.
// It also makes it safe to reference the service implementation projects in the host project to let Orleans locate the grain implementations;
// the types in the service implementation projects will not be available in the host project.

// Note that this grain has an internal contract - it is not accessible outside of the Tenant service assembly
// This ensures that User grains are only created via the grains that have a public contract - i.e. the Tenant grain
interface IUser : IGrainWithStringKey
{
    Task Update(User user);
    Task<User> Get();

    Task Clear();
}

sealed class UserGrain([PersistentState("state")] IPersistentState<UserGrain.State> state) : GrainBase<UserGrain.State>(state), IUser
{
    [GenerateSerializer]
    internal sealed class State
    {
        [Id(0)] public User User { get; set; } = new(Guid.Empty, string.Empty);
    }

    public async Task Update(User user)
    {
        ThrowIfNotEqualToKeyWithinTenant(user.Id);

        S.User = user;
        await state.WriteStateAsync();
    }

    public Task<User> Get() => Task.FromResult(S.User);

    public Task Clear() { state.State = new(); return state.ClearStateAsync(); }
}
