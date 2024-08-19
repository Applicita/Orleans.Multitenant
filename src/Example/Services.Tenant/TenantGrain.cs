namespace Orleans4Multitenant.Services.TenantService;

sealed class TenantGrain([PersistentState("state")] IPersistentState<TenantGrain.State> state) : GrainBase<TenantGrain.State>(state), ITenant
{
    [GenerateSerializer]
    internal sealed class State
    {
        [Id(0)] public Tenant Tenant { get; set; } = new(string.Empty);
        [Id(1)] public Dictionary<Guid, User> Users { get; set; } = [];
    }

    public Task<Tenant> Get() => Task.FromResult(S.Tenant);

    public async Task Update(Tenant tenant)
    {
        S.Tenant = tenant;
        await state.WriteStateAsync();
    }

    /// <remarks><see cref="User.Id"/> is ignored in <paramref name="user"/> and set in the result/></remarks>
    public async Task<UserResult> CreateUser(User user)
    {
        if (user.Id != Guid.Empty) return Errors.IdIsNotEmpty(user.Id);

        var newUser = user with { Id = Guid.NewGuid(), Name = user.Name };

        await User(newUser.Id).Update(newUser);

        S.Users.Add(newUser.Id, newUser);
        await state.WriteStateAsync();

        return newUser;
    }

    public Task<UserResult> GetUser(Guid id) => Task.FromResult<UserResult>(
        S.Users.TryGetValue(id, out var user) ?
            user :
            Errors.UserNotFound(id));

    public Task<ImmutableArray<User>> GetUsers() => Task.FromResult(S.Users.Values.ToImmutableArray());

    public async Task<Result> UpdateUser(User user)
    {
        if (!S.Users.TryGetValue(user.Id, out var currentUser))
            return Errors.UserNotFound(user.Id);

        if (currentUser != user)
        {
            await User(user.Id).Update(user);

            S.Users[user.Id] = user;
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
