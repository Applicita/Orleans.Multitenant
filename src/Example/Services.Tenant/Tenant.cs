using Orleans;
using Orleans.Runtime;

namespace Orleans4Multitenant.Services.Tenant;

class Tenant : GrainBase<Tenant.State>, ITenant
{
    [GenerateSerializer]
    internal class State
    {
        [Id(0)] public TenantInfo Info { get; set; } = new(string.Empty);
        [Id(1)] public Dictionary<Guid, UserInfo> Users { get; set; } = new();
    }

    public Tenant([PersistentState("state")] IPersistentState<State> state) : base(state) { }

    public Task<TenantInfo> GetInfo() => Task.FromResult(S.Info);

    public async Task Update(TenantInfo info)
    {
        S.Info = info;
        await state.WriteStateAsync();
    }

    /// <remarks><see cref="UserInfo.Id"/> is ignored in <paramref name="info"/> and set in the result/></remarks>
    public async Task<UserInfoResult> CreateUser(UserInfo info)
    {
        if (info.Id != Guid.Empty) return Errors.IdIsNotEmpty(info.Id);

        var newUserInfo = info with { Id = Guid.NewGuid(), Name = info.Name };

        await User(newUserInfo.Id).Update(newUserInfo);

        S.Users.Add(newUserInfo.Id, newUserInfo);
        await state.WriteStateAsync();

        return newUserInfo;
    }

    public Task<UserInfoResult> GetUser(Guid id) => Task.FromResult<UserInfoResult>(
        S.Users.TryGetValue(id, out var info) ?
            info :
            Errors.UserNotFound(id));

    public Task<ImmutableArray<UserInfo>> GetUsers() => Task.FromResult(S.Users.Values.ToImmutableArray());

    public async Task<Result> UpdateUser(UserInfo info)
    {
        if (!S.Users.TryGetValue(info.Id, out var currentInfo))
            return Errors.UserNotFound(info.Id);

        if (currentInfo != info)
        {
            await User(info.Id).Update(info);

            S.Users[info.Id] = info;
            await state.WriteStateAsync();
        }

        return Result.Ok;
    }

    public async Task<Result> DeleteUser(Guid id)
    {
        if (!S.Users.ContainsKey(id))
            return Errors.UserNotFound(id);

        await User(id).Clear();

        _ = S.Users.Remove(id);
        await state.WriteStateAsync();
        return Result.Ok;
    }

    IUser User(Guid id) => GetGrain<IUser>(id);
}
