using Orleans;
using Orleans.Runtime;

namespace Orleans4Multitenant.Services.Tenant;

// Note that this grain has an internal contract - it is not accessible outside of the Tenant service assembly
// This ensures that User grains are only created via the grains that have a public contract - i.e. the Tenant grain

// All public grain contracts for all services must be in the Contracts assembly
// All grain contracts in a service assembly must be internal
interface IUser : IGrainWithStringKey
{
    Task Update(UserInfo info);
    Task<UserInfo> GetInfo();

    Task Clear();
}

class User : GrainBase<User.State>, IUser
{
    [GenerateSerializer]
    internal class State
    {
        [Id(0)] public UserInfo Info { get; set; } = new(Guid.Empty, string.Empty);
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
