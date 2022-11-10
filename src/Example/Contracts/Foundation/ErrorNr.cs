namespace Orleans4Multitenant.Contracts;

[Flags]
public enum ErrorNr
{
    UserNotFound = 1,

    ValidationError = 1024,
    IdIsNotEmpty = 1 | ValidationError
}
