namespace Orleans4Multitenant.Contracts;


[Flags]
public enum ErrorCode
{
    UserNotFound = 1,

    ValidationError = 1024,
    IdIsNotEmpty = 1 | ValidationError
}
