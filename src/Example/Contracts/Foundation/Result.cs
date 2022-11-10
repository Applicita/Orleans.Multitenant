// Version: 1.0.0 (Using https://semver.org/)
// Updated: 2022-11-10
// See https://github.com/Applicita/Orleans.Results for updates to this file.

using System.Diagnostics.CodeAnalysis;

namespace Orleans4Multitenant.Contracts;

/// <summary>
/// Result without value; use to return either <see cref="Ok"/> or <see cref="ResultBase{ErrorNr}.Error"/>(s)
/// </summary>
[GenerateSerializer, Immutable]
public class Result : ResultBase<ErrorNr>
{
    public static Result Ok { get; } = new();

    public Result(ImmutableArray<Error> errors) : base(errors) { }
    public Result(IEnumerable<Error> errors) : base(ImmutableArray.CreateRange(errors)) { }
    Result() { }
    Result(Error error) : base(error) { }

    public static implicit operator Result(Error error) => new(error);
    public static implicit operator Result(ErrorNr nr) => new(nr);
    public static implicit operator Result((ErrorNr nr, string message) error) => new(error);
    public static implicit operator Result(List<Error> errors) => new(errors);
}

/// <summary>
/// Result with value; use to return either a <typeparamref name="TValue"/> or <see cref="ResultBase{ErrorNr}.Error"/>(s)
/// </summary>
[GenerateSerializer]
public class Result<TValue> : ResultBase<ErrorNr, TValue>
{
    public Result(ImmutableArray<Error> errors) : base(errors) { }
    public Result(IEnumerable<Error> errors) : base(ImmutableArray.CreateRange(errors)) { }
    Result(TValue value) : base(value) { }
    Result(Error error) : base(error) { }

    public static implicit operator Result<TValue>(TValue value) => new(value);
    public static implicit operator Result<TValue>(Error error) => new(error);
    public static implicit operator Result<TValue>(ErrorNr nr) => new(nr);
    public static implicit operator Result<TValue>((ErrorNr nr, string message) error) => new(error);
    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);
}

[GenerateSerializer]
public abstract class ResultBase<TErrorNr, TValue> : ResultBase<TErrorNr> where TErrorNr : Enum
{
    [Id(0)] TValue? value;

    protected ResultBase(TValue value) => this.value = value;
    protected ResultBase(Error error) : base(error) { }
    protected ResultBase(ImmutableArray<Error> errors) : base(errors) { }

    /// <summary>
    /// Returns the value for a success result, or the <typeparamref name="TValue"/> default for a failed result
    /// </summary>
    public TValue? ValueOrDefault => value;

    /// <summary>
    /// Get or set the value for a success result; throws <see cref="InvalidOperationException"/> for a failed result
    /// </summary>
    public TValue Value
    {
        get
        {
            ThrowIfFailed();
            return value!;
        }

        set
        {
            ThrowIfFailed();
            this.value = value;
        }
    }

    public override string ToString() => IsSuccess ? $"{Value}" : ErrorsText;

    void ThrowIfFailed() { if (IsFailed) throw new InvalidOperationException("Attempt to access the value of a failed result"); }
}

[GenerateSerializer]
public abstract class ResultBase<TErrorNr> where TErrorNr : Enum
{
    public bool IsSuccess => !IsFailed;
    public bool IsFailed => errors?.Length > 0;

    [Id(0)]
    readonly ImmutableArray<Error>? errors;

    /// <summary>
    /// Returns the errors for a failed result; throws an <see cref="InvalidOperationException"/> for a success result
    /// </summary>
    public ImmutableArray<Error> Errors => errors ?? throw new InvalidOperationException("Attempt to access the errors of a success result");

    /// <summary>
    /// Returns the error nr for a failed result with a single error; otherwise throws an exception
    /// </summary>
    public TErrorNr ErrorNr => Errors.Single().Nr;

    /// <summary>
    /// Returns all errors formatted in a single string for a failed result; throws an <see cref="InvalidOperationException"/> for a success result
    /// </summary>
    public string ErrorsText => string.Join(Environment.NewLine, Errors);

    /// <summary>
    /// Supports serializing validation errors into a https://tools.ietf.org/html/rfc7807 based problem details format
    /// </summary>
    /// <remarks>Intended for use with <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails"/> (in MVC controllers) or <see cref="Microsoft.AspNetCore.Http.Results.ValidationProblem"/> (in minimal api's) </remarks>
    /// <param name="validationErrorFlag">The enum flag used to identify an error as a validation error</param>
    /// <param name="validationErrors">If the return value is true, receives all errors in a dictionary suitable for serializing into a https://tools.ietf.org/html/rfc7807 based format; otherwise set to null</param>
    /// <returns>True for a failed result that has the <paramref name="validationErrorFlag"/> set in the <typeparamref name="TErrorNr"/> for <b>all</b> errors; false otherwise</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0001:Simplify Names", Justification = "Full name is necessary to ensure link works independently of global usings")]
    public bool TryAsValidationErrors(TErrorNr validationErrorFlag, [NotNullWhen(true)] out Dictionary<string, string[]>? validationErrors)
    {
        if (IsFailed && Errors.All(error => error.Nr.HasFlag(validationErrorFlag)))
        {
            validationErrors = new(Errors
                .GroupBy(error => error.Nr, error => error.Message)
                .Select(group => new KeyValuePair<string, string[]>(group.Key.ToString(), group.ToArray())));
            return true;
        }
        validationErrors = null;
        return false;
    }

    public override string ToString() => IsSuccess ? nameof(Result.Ok) : ErrorsText;

    protected ResultBase() { }
    protected ResultBase(Error error) => errors = ImmutableArray.Create(error);
    protected ResultBase(ImmutableArray<Error> errors) => this.errors = errors;

    /// <returns>A <see cref="NotImplementedException"/> with <paramref name="message"/> and <see cref="ErrorsText"/> for a failed result; <b>throws</b> an <see cref="InvalidOperationException"/> exception for a success result</returns>
    public NotImplementedException UnhandledErrorException(string? message = null) => new($"{message}Unhandled error(s): " + ErrorsText);

    [GenerateSerializer, Immutable]
    public record Error([property: Id(0)] TErrorNr Nr, [property: Id(1)] string Message = "")
    {
        public static implicit operator Error(TErrorNr nr) => new(nr);
        public static implicit operator Error((TErrorNr nr, string message) error) => new(error.nr, error.message);
    }
}
