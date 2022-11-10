namespace Orleans4Multitenant.Services;

static class Errors
{
    internal static Result.Error UserNotFound(Guid id) => new(ErrorNr.UserNotFound, $"User {id} not found");
    internal static Result.Error IdIsNotEmpty(Guid id) => new(ErrorNr.IdIsNotEmpty, $"{id} is not the empty guid");
}
