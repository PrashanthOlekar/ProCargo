using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Services;

public enum PasswordCheckResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}

/// <summary>Industry-standard password hashing (ASP.NET Core Identity PBKDF2 implementation).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheckResult Verify(string hashedPassword, string providedPassword);
}

/// <summary>Claims put into an access token.</summary>
public sealed record TokenSubject(
    long UserId,
    string Email,
    string FullName,
    PortalType Portal,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    long? CustomerId,
    long? OwnerId,
    long? DriverId,
    bool MustChangePassword);

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Issues short-lived JWT access tokens and opaque, high-entropy refresh / reset tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(TokenSubject subject);

    /// <summary>Cryptographically random URL-safe token (256 bits).</summary>
    string GenerateOpaqueToken();

    /// <summary>SHA-256 of an opaque token. Only hashes are ever stored in the database.</summary>
    byte[] HashOpaqueToken(string token);

    /// <summary>Refresh token lifetime for a portal (operations sessions are shorter).</summary>
    TimeSpan GetRefreshTokenLifetime(PortalType portal);
}

/// <summary>Encrypts sensitive values (PAN, bank account numbers) before they are stored.</summary>
public interface IFieldEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

/// <summary>Generates and hashes one-time passwords for pickup / delivery verification.</summary>
public interface IOtpService
{
    string GenerateOtp();
    byte[] HashOtp(long tripId, VerificationType type, string otp);
    bool Verify(long tripId, VerificationType type, string otp, byte[] expectedHash);
}
