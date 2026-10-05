using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// Drivers. Owners onboard drivers into their own fleet; drivers can also self-register (independent).
/// Access: the driver, the employing owner (ManageOwnFleet), or staff (ViewDrivers / ManageDrivers / ApproveDrivers).
/// </summary>
public sealed class DriverService : IDriverService
{
    private readonly IDriverRepository _drivers;
    private readonly IOwnerRepository _owners;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IAccountInviteService _invites;
    private readonly VerificationRules _verification;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public DriverService(IDriverRepository drivers, IOwnerRepository owners, IPasswordHasher passwordHasher, ITokenService tokens,
        IAccountInviteService invites, VerificationRules verification, INotificationService notifications, AccessGuard access,
        IAuditLogger audit, TimeProvider clock)
    {
        _drivers = drivers;
        _owners = owners;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _invites = invites;
        _verification = verification;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public Task<PagedResult<DriverListItemDto>> GetPagedAsync(DriverSearchRequest request, CancellationToken cancellationToken)
    {
        if (_access.IsStaffWith(Permissions.ViewDrivers))
        {
            return _drivers.GetPagedAsync(request, null, cancellationToken);
        }

        // Owners only ever see their own drivers.
        var ownerId = _access.RequireOwnerId();
        return _drivers.GetPagedAsync(request, ownerId, cancellationToken);
    }

    public async Task<DriverDto> GetByIdAsync(long driverId, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken) ?? throw NotFoundException.For("Driver", driverId);
        _access.EnsureDriver(driver.DriverId, driver.OwnerId, Permissions.ViewDrivers);
        return driver;
    }

    public Task<DriverDto> GetMineAsync(CancellationToken cancellationToken) =>
        GetByIdAsync(_access.RequireDriverId(), cancellationToken);

    public async Task<CreatedResponse> CreateAsync(CreateDriverRequest request, CancellationToken cancellationToken)
    {
        long? ownerId;
        if (_access.IsStaffWith(Permissions.ManageDrivers))
        {
            ownerId = request.OwnerId;
            if (ownerId is not null)
            {
                _ = await _owners.GetByIdAsync(ownerId.Value, cancellationToken) ?? throw NotFoundException.For("Owner", ownerId.Value);
            }
        }
        else
        {
            ownerId = _access.RequireOwnerId();
            if (!_access.User.HasPermission(Permissions.ManageOwnFleet)) throw new ForbiddenException();
        }

        var email = request.Email.Trim();
        var result = await _drivers.CreateAsync(new CreateDriverCommand(
            ownerId,
            request.FullName.Trim(),
            email,
            email.ToUpperInvariant(),
            IndianFormats.NormalizePhone(request.PhoneNumber),
            string.IsNullOrWhiteSpace(request.AlternatePhoneNumber) ? null : IndianFormats.NormalizePhone(request.AlternatePhoneNumber),
            request.DateOfBirth,
            _passwordHasher.Hash(_tokens.GenerateOpaqueToken()),
            NormalizeLicense(request.LicenseNumber),
            request.LicenseClass.Trim(),
            request.LicenseIssueDate,
            request.LicenseExpiryDate,
            request.IssuingAuthority?.Trim(),
            _access.User.UserId), cancellationToken);

        await _audit.LogAsync("DriverCreated", "Driver", result.ProfileId, newValue: new { result.ProfileNumber, OwnerId = ownerId },
            cancellationToken: cancellationToken);
        await _invites.SendInviteAsync(result.UserId, PortalType.Web, cancellationToken);

        return new CreatedResponse(result.ProfileId, result.ProfileNumber);
    }

    public async Task UpdateAsync(long driverId, UpdateDriverRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(driverId, cancellationToken);
        await _drivers.UpdateAsync(driverId, request, IndianFormats.NormalizePhone(request.PhoneNumber),
            string.IsNullOrWhiteSpace(request.AlternatePhoneNumber) ? null : IndianFormats.NormalizePhone(request.AlternatePhoneNumber),
            _access.User.UserId, cancellationToken);
        await _audit.LogAsync("DriverUpdated", "Driver", driverId, newValue: new { request.FullName }, cancellationToken: cancellationToken);
    }

    public async Task SetLicenseAsync(long driverId, SaveDriverLicenseRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(driverId, cancellationToken);
        request.LicenseNumber = NormalizeLicense(request.LicenseNumber);
        await _drivers.SetLicenseAsync(driverId, request, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("DriverLicenseUpdated", "Driver", driverId,
            newValue: new { request.LicenseClass, request.ExpiryDate }, cancellationToken: cancellationToken);
    }

    public async Task SetAvailabilityAsync(long driverId, SetDriverAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var driver = await EnsureCanManageAsync(driverId, cancellationToken);

        if (request.Status == DriverAvailabilityStatus.Available)
        {
            if (driver.VerificationStatusId != (int)VerificationStatus.Verified)
            {
                throw new BusinessRuleException("DRIVER_NOT_VERIFIED", "Only verified drivers can be marked available.");
            }

            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (driver.LicenseExpiryDate is null || driver.LicenseExpiryDate < today)
            {
                throw new BusinessRuleException("LICENSE_EXPIRED", "The driving licence is missing or expired.");
            }
        }

        await _drivers.SetAvailabilityAsync(driverId, request.Status, request.Reason, _access.User.UserId, cancellationToken);
    }

    public async Task VerifyAsync(long driverId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ApproveDrivers);
        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken) ?? throw NotFoundException.For("Driver", driverId);

        if (request.Status == VerificationStatus.Verified)
        {
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (driver.LicenseExpiryDate is null || driver.LicenseExpiryDate < today)
            {
                throw new BusinessRuleException("LICENSE_EXPIRED", "A valid driving licence is required before verification.");
            }

            await _verification.EnsureMandatoryDocumentsVerifiedAsync(DocumentEntityType.Driver, driverId, cancellationToken);
        }

        await _drivers.SetVerificationAsync(driverId, request.Status, request.Remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("DriverVerification", "Driver", driverId, oldValue: new { driver.VerificationStatusId },
            newValue: new { Status = request.Status.ToString(), request.Remarks }, cancellationToken: cancellationToken);
        await _notifications.NotifyUserAsync(driver.UserId, NotificationTemplates.VerificationUpdated,
            VerificationRules.NotificationData("driver profile", new(request.Status, request.Remarks)),
            new NotificationSubject("Driver", driverId), cancellationToken);
    }

    public async Task SetActiveAsync(long driverId, bool isActive, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(driverId, cancellationToken, staffPermission: Permissions.ManageDrivers, allowDriverSelf: false);
        await _drivers.SetActiveAsync(driverId, isActive, _access.User.UserId, cancellationToken);
        await _audit.LogAsync(isActive ? "DriverActivated" : "DriverDeactivated", "Driver", driverId, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AvailableDriverDto>> GetAvailableForAssignmentAsync(long? ownerId, DateTime onDateUtc, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.AssignTrips);
        return _drivers.GetAvailableForAssignmentAsync(ownerId, onDateUtc, cancellationToken);
    }

    /// <summary>The driver themself, the employing owner, or staff with ManageDrivers may change a driver.</summary>
    private async Task<DriverDto> EnsureCanManageAsync(long driverId, CancellationToken cancellationToken,
        string staffPermission = Permissions.ManageDrivers, bool allowDriverSelf = true)
    {
        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken) ?? throw NotFoundException.For("Driver", driverId);

        if (_access.IsStaffWith(staffPermission)) return driver;
        if (allowDriverSelf && _access.User.DriverId == driverId) return driver;
        if (_access.User.OwnerId is not null && driver.OwnerId == _access.User.OwnerId && _access.User.HasPermission(Permissions.ManageOwnFleet))
        {
            return driver;
        }

        throw NotFoundException.For("Driver", driverId);
    }

    private static string NormalizeLicense(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
