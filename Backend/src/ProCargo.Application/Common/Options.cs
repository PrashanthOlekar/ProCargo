namespace ProCargo.Application.Common;

/// <summary>JWT settings (section "Jwt"). SigningKey comes from Key Vault / user-secrets, never appsettings.json.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://api.procargo.com";
    public string WebAudience { get; set; } = "procargo-web";
    public string OperationsAudience { get; set; } = "procargo-operations";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int WebRefreshTokenDays { get; set; } = 14;
    public int OperationsRefreshTokenHours { get; set; } = 12;
}

/// <summary>Account protection settings (section "Security").</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int PasswordResetTokenMinutes { get; set; } = 30;
    public int InviteTokenMinutes { get; set; } = 1440;

    /// <summary>Secret used to HMAC pickup/delivery OTPs (Key Vault).</summary>
    public string OtpHashingKey { get; set; } = string.Empty;

    /// <summary>Minimum seconds between two OTP sends for the same trip step.</summary>
    public int OtpResendSeconds { get; set; } = 45;

    /// <summary>
    /// Development/test only: return trip OTPs in the API response so the flow can be exercised without an SMS
    /// provider. Startup refuses to run with this enabled in Production. OTPs are never logged.
    /// </summary>
    public bool ExposeOtpForTesting { get; set; }
}

/// <summary>Public URLs of the two front-end solutions (section "Portals"), used in e-mail links.</summary>
public sealed class PortalOptions
{
    public const string SectionName = "Portals";

    public string WebBaseUrl { get; set; } = "https://www.procargo.com";
    public string OperationsBaseUrl { get; set; } = "https://operations.procargo.com";
}

/// <summary>Upload restrictions (section "FileUpload").</summary>
public sealed class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxFilesPerRequest { get; set; } = 5;

    /// <summary>Extension -> allowed MIME types.</summary>
    public Dictionary<string, string[]> AllowedTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"]
    };
}

/// <summary>Payment settings (section "Payments").</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Sandbox (development) or Razorpay.</summary>
    public string Gateway { get; set; } = "Sandbox";
    public string Currency { get; set; } = "INR";
}
