using Microsoft.AspNetCore.Mvc;
using Orleans4Multitenant.Contracts;
using Orleans4Multitenant.Contracts.TenantContract;

namespace Orleans4Multitenant.Apis.TenantApi;

[ApiController]
public class TenantController : ControllerBase
{
    const string Tenant   = "tenant";
    const string Users  = "users";
    const string UserId = "users/{id}";

    public TenantController(IClusterClient orleans) : base(orleans) { }

    /// <response code="200">The tenant has been updated</response>
    [HttpPut(Tenant)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Update(Tenant tenant)
     => await RequestTenant.Update(tenant);

    /// <response code="200">Returns the tenant</response>
    [HttpGet(Tenant)]
    public async Task<Tenant> Get()
     => await RequestTenant.Get();

    /// <param name="user">The specified id must be the empty guid: 00000000-0000-0000-0000-000000000000</param>
    /// <response code="201">Returns the new user</response>
    /// <response code="400">If the specified id is not the empty guid</response>
    [HttpPost(Users)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        var result = await RequestTenant.CreateUser(user);
        return result.TryAsValidationErrors(ErrorNr.ValidationError, out var validationErrors)
            ? ValidationProblem(new ValidationProblemDetails(validationErrors))
            : result switch
            {
                { IsSuccess: true } r => CreatedAtAction(nameof(CreateUser), r.Value),
                {                 } r => throw r.UnhandledErrorException()
            };
    }

    /// <response code="200">Returns all users</response>
    [HttpGet(Users)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
     => Ok(await RequestTenant.GetUsers());

    /// <response code="200">Returns the user</response>
    /// <response code="404">If the user is not found</response>
    [HttpGet(UserId)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<User>> GetUser(Guid id)
     => await RequestTenant.GetUser(id) switch
        {
            { IsSuccess: true               } r => Ok(r.Value),
            { ErrorNr: ErrorNr.UserNotFound } r => NotFound(r.ErrorsText),
            {                               } r => throw r.UnhandledErrorException()
        };

    /// <param name="id">must be equal to id in <paramref name="user"/></param>
    /// <param name="user">id must be equal to id in the url<paramref name="user"/></param>
    /// <response code="200">If the user has been updated</response>
    /// <response code="404">If the user is not found</response>
    [HttpPut(UserId)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateUser(Guid id, User user)
     => id != user.Id ? BadRequest($"url id {id} != user id {user?.Id}") :
        await RequestTenant.UpdateUser(user) switch
        {
            { IsSuccess: true               }   => Ok(),
            { ErrorNr: ErrorNr.UserNotFound } r => NotFound(r.ErrorsText),
            {                               } r => throw r.UnhandledErrorException()
        };

    /// <response code="200">If the user has been deleted</response>
    /// <response code="404">If the user is not found</response>
    [HttpDelete(UserId)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid id)
     => await RequestTenant.DeleteUser(id) switch
        {
            { IsSuccess: true               }   => Ok(),
            { ErrorNr: ErrorNr.UserNotFound } r => NotFound(r.ErrorsText),
            {                               } r => throw r.UnhandledErrorException()
        };
}
