namespace ProCargo.Application.Responses;

/// <summary>
/// Error body returned for every non-2xx response:
/// { "success": false, "message": "...", "errorCode": "BOOKING_NOT_FOUND", "errors": {..}, "traceId": "00-..." }
/// Successful responses return the resource itself (or a PagedResult) without a wrapper.
/// </summary>
public sealed record ErrorResponse(
    bool Success,
    string Message,
    string ErrorCode,
    IReadOnlyDictionary<string, string[]>? Errors,
    string? TraceId)
{
    public static ErrorResponse Create(string message, string errorCode, string? traceId,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, message, errorCode, errors, traceId);
}

/// <summary>Body of simple command endpoints that report an outcome message.</summary>
public sealed record MessageResponse(bool Success, string Message)
{
    public static MessageResponse Ok(string message) => new(true, message);
}
