namespace Accounting.Application.Common;

/// <summary>
/// Represents the type of error that occurred.
/// Maps to HTTP status codes for API responses.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Validation error - invalid input data (HTTP 400).
    /// </summary>
    Validation,

    /// <summary>
    /// Resource not found (HTTP 404).
    /// </summary>
    NotFound,

    /// <summary>
    /// Conflict with existing data, e.g., duplicate resource (HTTP 409).
    /// </summary>
    Conflict,

    /// <summary>
    /// General business rule violation or system failure (HTTP 500).
    /// </summary>
    Failure
}

/// <summary>
/// Represents an error with a type, code, and message.
/// </summary>
public sealed record Error
{
    /// <summary>
    /// The type of error.
    /// </summary>
    public ErrorType Type { get; init; }

    /// <summary>
    /// A machine-readable error code (e.g., "DUPLICATE_RIDE", "ACCOUNT_NOT_FOUND").
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// A human-readable error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Optional additional details about the error.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }

    private Error(ErrorType type, string code, string message, Dictionary<string, object>? details = null)
    {
        Type = type;
        Code = code;
        Message = message;
        Details = details;
    }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string code, string message, Dictionary<string, object>? details = null)
        => new(ErrorType.Validation, code, message, details);

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string code, string message, Dictionary<string, object>? details = null)
        => new(ErrorType.NotFound, code, message, details);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string code, string message, Dictionary<string, object>? details = null)
        => new(ErrorType.Conflict, code, message, details);

    /// <summary>
    /// Creates a general failure error.
    /// </summary>
    public static Error Failure(string code, string message, Dictionary<string, object>? details = null)
        => new(ErrorType.Failure, code, message, details);
}

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error if the operation failed; otherwise, null.
    /// </summary>
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error != null)
        {
            throw new InvalidOperationException("Cannot have both success and error.");
        }

        if (!isSuccess && error == null)
        {
            throw new InvalidOperationException("Failure must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, null);

    /// <summary>
    /// Creates a failed result with a value type.
    /// </summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);
}

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="TValue">The type of the value returned on success.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessing value when operation failed.</exception>
    public TValue Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("Cannot access value of a failed result.");
            }

            return _value!;
        }
    }

    internal Result(TValue? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Implicitly converts a value to a successful Result.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// Implicitly converts an Error to a failed Result.
    /// </summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
