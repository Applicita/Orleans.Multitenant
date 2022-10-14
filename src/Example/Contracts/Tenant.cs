using Orleans;
namespace Orleans4Multitenant.Contracts.Tenant;

[GenerateSerializer, Immutable]
public record TenantInfo([property: Id(0)] string Name);

[GenerateSerializer, Immutable]
public record UserInfo([property: Id(0)] Guid Id, [property: Id(1)] string Name);

public interface ITenant : IGrainWithStringKey
{
    /// <summary>Identifies the only instance of this grain</summary>
    const string Id = "";
    Task Update(TenantInfo info);
    Task<TenantInfo> GetInfo();

    Task<UserInfoResult> CreateUser(UserInfo info);
    Task<UserInfoResult> GetUser(Guid id);
    Task<ImmutableArray<UserInfo>> GetUsers();
    Task<Result> UpdateUser(UserInfo info);
    Task<Result> DeleteUser(Guid id);
}
