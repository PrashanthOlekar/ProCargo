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
/// Vehicle owner profile, business details, addresses, bank accounts and KYC verification.
/// PAN and bank account numbers are encrypted before persistence; only the last four characters
/// are ever returned, except through the audited "reveal" used by Finance to make a payout.
/// </summary>
public sealed class OwnerService : IOwnerService
{
    private readonly IOwnerRepository _owners;
    private readonly IMasterDataRepository _masterData;
    private readonly IFieldEncryptor _encryptor;
    private readonly VerificationRules _verification;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;

    public OwnerService(IOwnerRepository owners, IMasterDataRepository masterData, IFieldEncryptor encryptor, VerificationRules verification,
        INotificationService notifications, AccessGuard access, IAuditLogger audit)
    {
        _owners = owners;
        _masterData = masterData;
        _encryptor = encryptor;
        _verification = verification;
        _notifications = notifications;
        _access = access;
        _audit = audit;
    }

    public Task<PagedResult<OwnerListItemDto>> GetPagedAsync(OwnerSearchRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewOwners);
        return _owners.GetPagedAsync(request, cancellationToken);
    }

    public async Task<OwnerProfileResponse> GetByIdAsync(long ownerId, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ViewOwners);
        var owner = await _owners.GetByIdAsync(ownerId, cancellationToken) ?? throw NotFoundException.For("Owner", ownerId);
        var addresses = await _owners.GetAddressesAsync(ownerId, cancellationToken);
        var accounts = await _owners.GetBankAccountsAsync(ownerId, cancellationToken);
        return new OwnerProfileResponse(owner, addresses, accounts);
    }

    public Task<OwnerProfileResponse> GetMineAsync(CancellationToken cancellationToken) =>
        GetByIdAsync(_access.RequireOwnerId(), cancellationToken);

    public async Task UpdateAsync(long ownerId, UpdateOwnerRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);

        var pan = string.IsNullOrWhiteSpace(request.PanNumber) ? null : request.PanNumber.Trim().ToUpperInvariant();
        await _owners.UpdateAsync(new UpdateOwnerCommand(
            ownerId,
            request.OwnerType,
            request.FullName.Trim(),
            IndianFormats.NormalizePhone(request.PhoneNumber),
            UpdatePan: pan is not null,
            PanEncrypted: pan is null ? null : _encryptor.Encrypt(pan),
            PanLast4: pan is null ? null : IndianFormats.LastFour(pan),
            request.RowVersion,
            _access.User.UserId), cancellationToken);

        await _audit.LogAsync("OwnerUpdated", "Owner", ownerId,
            newValue: new { request.FullName, request.OwnerType, PanChanged = pan is not null }, cancellationToken: cancellationToken);
    }

    public async Task SaveBusinessAsync(long ownerId, SaveOwnerBusinessRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);
        await _owners.SaveBusinessAsync(ownerId, request, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("OwnerBusinessUpdated", "Owner", ownerId, newValue: request, cancellationToken: cancellationToken);
    }

    public async Task<long> SaveAddressAsync(long ownerId, long? addressId, SaveOwnerAddressRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);
        _ = await _masterData.GetCityAsync(request.CityId, cancellationToken)
            ?? throw new BusinessRuleException("CITY_INVALID", "The selected city does not exist.");
        return await _owners.SaveAddressAsync(ownerId, addressId, request, _access.User.UserId, cancellationToken);
    }

    public Task DeleteAddressAsync(long ownerId, long addressId, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);
        return _owners.DeleteAddressAsync(ownerId, addressId, _access.User.UserId, cancellationToken);
    }

    public async Task<long> CreateBankAccountAsync(long ownerId, CreateBankAccountRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);

        var accountNumber = request.AccountNumber.Trim();
        var id = await _owners.CreateBankAccountAsync(new CreateBankAccountCommand(
            ownerId,
            request.AccountHolderName.Trim(),
            request.BankName.Trim(),
            _encryptor.Encrypt(accountNumber),
            IndianFormats.LastFour(accountNumber),
            request.IfscCode.Trim().ToUpperInvariant(),
            request.IsPrimary,
            _access.User.UserId), cancellationToken);

        await _audit.LogAsync("OwnerBankAccountAdded", "Owner", ownerId,
            newValue: new { BankAccountId = id, Last4 = IndianFormats.LastFour(accountNumber), request.IfscCode }, cancellationToken: cancellationToken);
        return id;
    }

    public async Task DeactivateBankAccountAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken)
    {
        _access.EnsureOwner(ownerId, Permissions.ManageOwners);
        await _owners.DeactivateBankAccountAsync(ownerId, bankAccountId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("OwnerBankAccountRemoved", "Owner", ownerId, newValue: new { BankAccountId = bankAccountId }, cancellationToken: cancellationToken);
    }

    public async Task VerifyBankAccountAsync(long ownerId, long bankAccountId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ApproveOwners);
        await _owners.SetBankAccountVerificationAsync(ownerId, bankAccountId, request.Status, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("OwnerBankAccountVerification", "Owner", ownerId,
            newValue: new { BankAccountId = bankAccountId, Status = request.Status.ToString() }, cancellationToken: cancellationToken);
    }

    public async Task<BankAccountRevealResponse> RevealBankAccountAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var row = await _owners.GetBankAccountSensitiveAsync(ownerId, bankAccountId, cancellationToken)
                  ?? throw NotFoundException.For("BankAccount", bankAccountId);

        // Every reveal is audited (who saw which account and when). The value itself is never logged.
        await _audit.LogAsync("OwnerBankAccountRevealed", "Owner", ownerId, newValue: new { BankAccountId = bankAccountId },
            cancellationToken: cancellationToken);

        return new BankAccountRevealResponse(row.OwnerBankAccountId, row.AccountHolderName, row.BankName, row.IfscCode,
            _encryptor.Decrypt(row.AccountNumberEncrypted));
    }

    public async Task VerifyAsync(long ownerId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ApproveOwners);
        var owner = await _owners.GetByIdAsync(ownerId, cancellationToken) ?? throw NotFoundException.For("Owner", ownerId);

        if (request.Status == VerificationStatus.Verified)
        {
            await _verification.EnsureMandatoryDocumentsVerifiedAsync(DocumentEntityType.Owner, ownerId, cancellationToken);
        }

        await _owners.SetVerificationAsync(ownerId, request.Status, request.Remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("OwnerVerification", "Owner", ownerId, oldValue: new { owner.VerificationStatusId },
            newValue: new { Status = request.Status.ToString(), request.Remarks }, cancellationToken: cancellationToken);
        await _notifications.NotifyUserAsync(owner.UserId, NotificationTemplates.VerificationUpdated,
            VerificationRules.NotificationData("owner profile", new(request.Status, request.Remarks)),
            new NotificationSubject("Owner", ownerId), cancellationToken);
    }

    public async Task SetActiveAsync(long ownerId, bool isActive, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageOwners);
        await _owners.SetActiveAsync(ownerId, isActive, _access.User.UserId, cancellationToken);
        await _audit.LogAsync(isActive ? "OwnerActivated" : "OwnerDeactivated", "Owner", ownerId, cancellationToken: cancellationToken);
    }
}
