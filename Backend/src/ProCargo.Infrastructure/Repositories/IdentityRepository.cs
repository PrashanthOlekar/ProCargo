using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

/// <summary>Authentication persistence via sec.usp_* procedures.</summary>
internal sealed class IdentityRepository : IIdentityRepository
{
    private readonly StoredProcedureExecutor _sp;

    public IdentityRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<UserAuthInfo?> GetAuthInfoByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<UserAuthInfo>(
            $"EXEC sec.usp_User_GetAuthInfo @NormalizedEmail={normalizedEmail}, @UserId={(long?)null}", cancellationToken);

    public Task<UserAuthInfo?> GetAuthInfoByIdAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<UserAuthInfo>(
            $"EXEC sec.usp_User_GetAuthInfo @NormalizedEmail={(string?)null}, @UserId={userId}", cancellationToken);

    public Task<IReadOnlyList<RolePermissionRow>> GetRolesAndPermissionsAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<RolePermissionRow>($"EXEC sec.usp_User_GetRolesAndPermissions @UserId={userId}", cancellationToken);

    public Task<RegistrationResult> RegisterAsync(RegisterUserCommand c, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<RegistrationResult>($"""
            EXEC sec.usp_User_Register
                @Email={c.Email}, @NormalizedEmail={c.NormalizedEmail}, @PhoneNumber={c.PhoneNumber}, @FullName={c.FullName},
                @PasswordHash={c.PasswordHash}, @AccountType={c.AccountType.ToString()},
                @CustomerTypeId={(int?)c.CustomerType}, @CompanyName={c.CompanyName},
                @OwnerTypeId={(int?)c.OwnerType}, @BusinessName={c.BusinessName}, @GstNumber={c.GstNumber}
            """, cancellationToken);

    public async Task<bool> EnsureSuperAdminAsync(string email, string normalizedEmail, string phone, string fullName, string passwordHash,
        CancellationToken cancellationToken)
    {
        var row = await _sp.QuerySingleAsync<BoolValue>($"""
            EXEC sec.usp_User_EnsureSuperAdmin @Email={email}, @NormalizedEmail={normalizedEmail}, @PhoneNumber={phone},
                @FullName={fullName}, @PasswordHash={passwordHash}
            """, cancellationToken);
        return row.Value;
    }

    public Task RecordLoginSuccessAsync(LoginAttempt a, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_User_RecordLoginSuccess @UserId={a.UserId}, @AttemptedEmail={a.AttemptedEmail}, @Portal={a.Portal.ToString()},
                @IpAddress={a.IpAddress}, @UserAgent={a.UserAgent}
            """, cancellationToken);

    public Task<LoginFailureResult> RecordLoginFailureAsync(LoginAttempt a, string reason, bool countsTowardLockout, int maxFailedAttempts,
        int lockoutMinutes, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<LoginFailureResult>($"""
            EXEC sec.usp_User_RecordLoginFailure @UserId={a.UserId}, @AttemptedEmail={a.AttemptedEmail}, @Portal={a.Portal.ToString()},
                @FailureReason={reason}, @IpAddress={a.IpAddress}, @UserAgent={a.UserAgent}, @CountsTowardLockout={countsTowardLockout},
                @MaxFailedAttempts={maxFailedAttempts}, @LockoutMinutes={lockoutMinutes}
            """, cancellationToken);

    public Task CreateRefreshTokenAsync(long userId, byte[] tokenHash, Guid familyId, PortalType portal, DateTime expiresUtc,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<IdResult>($"""
            EXEC sec.usp_RefreshToken_Create @UserId={userId}, @TokenHash={tokenHash}, @FamilyId={familyId}, @Portal={portal.ToString()},
                @ExpiresDateUtc={expiresUtc}, @CreatedByIp={ipAddress}, @UserAgent={userAgent}
            """, cancellationToken);

    public Task<RefreshTokenRecord?> GetRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<RefreshTokenRecord>($"EXEC sec.usp_RefreshToken_GetByHash @TokenHash={tokenHash}", cancellationToken);

    public Task RotateRefreshTokenAsync(long oldTokenId, byte[] newTokenHash, DateTime expiresUtc, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<IdResult>($"""
            EXEC sec.usp_RefreshToken_Rotate @OldRefreshTokenId={oldTokenId}, @NewTokenHash={newTokenHash}, @ExpiresDateUtc={expiresUtc},
                @CreatedByIp={ipAddress}, @UserAgent={userAgent}
            """, cancellationToken);

    public Task RevokeRefreshTokenFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_RefreshToken_RevokeFamily @FamilyId={familyId}, @Reason={reason}", cancellationToken);

    public Task RevokeRefreshTokenAsync(byte[] tokenHash, string reason, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_RefreshToken_Revoke @TokenHash={tokenHash}, @Reason={reason}", cancellationToken);

    public Task RevokeAllRefreshTokensAsync(long userId, string reason, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_RefreshToken_RevokeAllForUser @UserId={userId}, @Reason={reason}", cancellationToken);

    public Task CreatePasswordResetTokenAsync(long userId, byte[] tokenHash, DateTime expiresUtc, string? ipAddress, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_PasswordResetToken_Create @UserId={userId}, @TokenHash={tokenHash}, @ExpiresDateUtc={expiresUtc}, @RequestedIp={ipAddress}
            """, cancellationToken);

    public async Task<long> ConsumePasswordResetTokenAsync(byte[] tokenHash, string newPasswordHash, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>(
            $"EXEC sec.usp_PasswordResetToken_Consume @TokenHash={tokenHash}, @NewPasswordHash={newPasswordHash}", cancellationToken)).Id;

    public Task ChangePasswordAsync(long userId, string newPasswordHash, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_User_ChangePassword @UserId={userId}, @NewPasswordHash={newPasswordHash}", cancellationToken);

    internal sealed class BoolValue
    {
        public bool Value { get; init; }
    }
}
