using Orleans;
using Orleans.Runtime;

namespace Orleans4Multitenant.Services.Tenant;

// All public grain contracts for all services must be in the Contracts assembly, using a separate subnamespace for each service

// All grain contracts defined in a service assembly must be internal

// All interfaces and types defined in a service assembly must be non-public;
// the public keyword is *only* used on interface member implementations and grain constructors.
// This ensures that the only external code access is Orleans instantiating grains.
// It also makes it safe to reference the service implementation projects in the host project to let Orleans locate the grain implementations;
// the types in the service implementation projects will not be available in the host project.

// Note that this grain has an internal contract - it is not accessible outside of the Tenant service assembly
// This ensures that User grains are only created via the grains that have a public contract - i.e. the Tenant grain
interface IUser : IGrainWithStringKey
{
    Task Update(UserInfo info);
    Task<UserInfo> GetInfo();

    Task Clear();
}

sealed class User : GrainBase<User.State>, IUser
{
    [GenerateSerializer]
    internal sealed class State
    {
        [Id(0)] internal UserInfo Info { get; set; } = new(Guid.Empty, string.Empty);
    }

    public User([PersistentState("state")] IPersistentState<State> state) : base(state) { }

    public async Task Update(UserInfo info)
    {
        ThrowIfNotEqualToKeyWithinTenant(info.Id);

        S.Info = info;
        await state.WriteStateAsync();
    }

    public Task<UserInfo> GetInfo() => Task.FromResult(S.Info);

    public Task Clear() { state.State = new(); return state.ClearStateAsync(); }
}
