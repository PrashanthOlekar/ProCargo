using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProCargo.Application.Common;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 signed JWT access tokens. Each portal has its own audience, so a token issued to the customer
/// website is rejected by Operations endpoints and vice versa. Tokens are short-lived (default 15 minutes);
/// sessions continue through rotating refresh tokens kept in HttpOnly cookies.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        _credentials = new SigningCredentials(JwtSigningKey.Create(_options.SigningKey), SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(TokenSubject subject)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, subject.Email),
            new(JwtRegisteredClaimNames.Name, subject.FullName),
            new(ProCargoClaimTypes.Portal, subject.Portal.ToString())
        };

        claims.AddRange(subject.Roles.Select(r => new Claim(ProCargoClaimTypes.Role, r)));
        claims.AddRange(subject.Permissions.Select(p => new Claim(ProCargoClaimTypes.Permission, p)));
        if (subject.CustomerId is long cid) claims.Add(new Claim(ProCargoClaimTypes.CustomerId, cid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (subject.OwnerId is long oid) claims.Add(new Claim(ProCargoClaimTypes.OwnerId, oid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (subject.DriverId is long did) claims.Add(new Claim(ProCargoClaimTypes.DriverId, did.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (subject.MustChangePassword) claims.Add(new Claim(ProCargoClaimTypes.MustChangePassword, "true"));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = subject.Portal == PortalType.Operations ? _options.OperationsAudience : _options.WebAudience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials
        });

        return new AccessToken(token, expires);
    }

    public string GenerateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public byte[] HashOpaqueToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public TimeSpan GetRefreshTokenLifetime(PortalType portal) =>
        portal == PortalType.Operations
            ? TimeSpan.FromHours(_options.OperationsRefreshTokenHours)
            : TimeSpan.FromDays(_options.WebRefreshTokenDays);
}

/// <summary>Builds the symmetric signing key shared by token issuing and validation.</summary>
public static class JwtSigningKey
{
    public static SymmetricSecurityKey Create(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes (use user-secrets or Key Vault).");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)) { KeyId = "procargo-hs256" };
    }
}

/// <summary>ASP.NET Core Identity's PBKDF2 (HMAC-SHA512, 100k iterations, V3 format) password hasher.</summary>
internal sealed class IdentityPasswordHasher : Application.Interfaces.Services.IPasswordHasher
{
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public PasswordCheckResult Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(Subject, hashedPassword, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed
        };
}

/// <summary>
/// Field-level encryption for PAN and bank account numbers using ASP.NET Core Data Protection
/// (AES-256-CBC + HMACSHA256, keys rotated automatically; in Azure the key ring is stored in Blob Storage and
/// protected with Key Vault).
/// </summary>
internal sealed class DataProtectionFieldEncryptor : IFieldEncryptor
{
    private readonly IDataProtector _protector;

    public DataProtectionFieldEncryptor(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ProCargo.FieldEncryption.v1");
    }

    public string Encrypt(string plainText) => _protector.Protect(plainText);

    public string Decrypt(string cipherText) => _protector.Unprotect(cipherText);
}

/// <summary>
/// Six-digit OTPs from a CSPRNG. Only HMAC-SHA256(key, tripId:type:otp) is stored, so a database leak does not
/// reveal OTPs and a hash cannot be replayed against another trip or step. Comparison is constant-time.
/// </summary>
internal sealed class HmacOtpService : IOtpService
{
    private readonly byte[] _key;

    public HmacOtpService(IOptions<SecurityOptions> options)
    {
        var key = options.Value.OtpHashingKey;
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Security:OtpHashingKey must be configured with at least 32 bytes.");
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string GenerateOtp() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public byte[] HashOtp(long tripId, VerificationType type, string otp) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{tripId}:{(int)type}:{otp}"));

    public bool Verify(long tripId, VerificationType type, string otp, byte[] expectedHash) =>
        CryptographicOperations.FixedTimeEquals(HashOtp(tripId, type, otp), expectedHash);
}
