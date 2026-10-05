using ProCargo.Application.Common;

namespace ProCargo.Application.Exceptions;

/// <summary>
/// Base for all expected (handled) application errors. The API's exception middleware maps each
/// subtype to an HTTP status code and returns { success, message, errorCode, errors, traceId }.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>404</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string errorCode, string message) : base(errorCode, message) { }

    public static NotFoundException For(string entity, object id) =>
        new($"{entity.ToUpperInvariant()}_NOT_FOUND", $"{entity} '{id}' was not found.");
}

/// <summary>403 - authenticated but not allowed to touch this resource.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have access to this resource.")
        : base(ErrorCodes.Forbidden, message) { }
}

/// <summary>401 - credentials / tokens are missing or invalid.</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string errorCode, string message) : base(errorCode, message) { }
}

/// <summary>409 - duplicate or conflicting state.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string errorCode, string message) : base(errorCode, message) { }
}

/// <summary>409 - optimistic concurrency (rowversion / expected status) check failed.</summary>
public sealed class ConcurrencyException : AppException
{
    public ConcurrencyException(string message = "The record was modified by someone else. Reload and try again.")
        : base(ErrorCodes.ConcurrencyConflict, message) { }
}

/// <summary>422 - the request is well-formed but violates a business rule.</summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string errorCode, string message) : base(errorCode, message) { }
}

/// <summary>400 - request validation failed (FluentValidation or manual checks).</summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.ValidationFailed, "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>502 - an external provider (payment gateway, SMS) failed.</summary>
public sealed class ExternalServiceException : AppException
{
    public ExternalServiceException(string errorCode, string message) : base(errorCode, message) { }
}
