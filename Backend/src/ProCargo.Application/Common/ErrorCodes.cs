namespace ProCargo.Application.Common;

/// <summary>
/// Error codes raised by the application layer. Codes raised by stored procedures travel in the SQL error
/// message ("CODE|message") and are passed through unchanged, so clients see one consistent vocabulary.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Conflict = "CONFLICT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string DuplicateValue = "DUPLICATE_VALUE";
    public const string ReferenceInvalid = "REFERENCE_INVALID";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";

    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string PortalNotAllowed = "PORTAL_NOT_ALLOWED";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string CurrentPasswordInvalid = "CURRENT_PASSWORD_INVALID";

    public const string ProfileRequired = "PROFILE_REQUIRED";
    public const string OtpInvalid = "OTP_INVALID";
    public const string FileInvalid = "FILE_INVALID";
    public const string PricingNotConfigured = "PRICING_NOT_CONFIGURED";
    public const string DiscountApprovalRequired = "DISCOUNT_APPROVAL_REQUIRED";
    public const string PaymentGatewayError = "PAYMENT_GATEWAY_ERROR";
    public const string WebhookSignatureInvalid = "WEBHOOK_SIGNATURE_INVALID";
}
