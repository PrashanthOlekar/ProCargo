using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.DomainRules;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Finance;

/// <summary>
/// Owner settlements: trip freight (invoice sub-total) minus platform commission and TDS, plus adjustments.
///
/// Controls
///   * Only completed trips whose invoice is fully paid can be settled (SQL).
///   * Commission comes from the effective fin.CommissionRule for the vehicle type; TDS from settings.
///   * Maker-checker: the user who created a settlement cannot approve it (ApproveSettlements permission).
///   * Status moves only along Pending -> Approved -> Processing -> Completed/Failed (or Cancelled before processing),
///     with the expected status checked in SQL.
///   * Owners see only their own settlements and earnings.
/// </summary>
public sealed class SettlementService : ISettlementService
{
    private readonly ISettlementRepository _settlements;
    private readonly ITripRepository _trips;
    private readonly IPricingRepository _pricing;
    private readonly ISettingsProvider _settings;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public SettlementService(ISettlementRepository settlements, ITripRepository trips, IPricingRepository pricing, ISettingsProvider settings,
        INotificationService notifications, AccessGuard access, IAuditLogger audit, TimeProvider clock)
    {
        _settlements = settlements;
        _trips = trips;
        _pricing = pricing;
        _settings = settings;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public Task<PagedResult<SettlementListItemDto>> GetPagedAsync(SettlementSearchRequest request, CancellationToken cancellationToken)
    {
        var ownerScope = _access.IsStaffWith(Permissions.ViewFinance) ? request.OwnerId : _access.RequireOwnerId();
        return _settlements.GetPagedAsync(request, ownerScope, cancellationToken);
    }

    public async Task<SettlementDetailsResponse> GetByIdAsync(long settlementId, CancellationToken cancellationToken)
    {
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);
        var items = await _settlements.GetItemsAsync(settlementId, cancellationToken);
        return new SettlementDetailsResponse(settlement, items, AvailableActions(settlement));
    }

    public Task<PagedResult<SettlementEligibleTripDto>> GetEligibleTripsAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        return _settlements.GetEligibleTripsAsync(request, cancellationToken);
    }

    public async Task<CreatedResponse> CreateAsync(CreateSettlementRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);

        var trip = await _trips.GetByIdAsync(request.TripId, cancellationToken) ?? throw NotFoundException.For("Trip", request.TripId);
        var rule = await _pricing.GetCommissionRuleAsync(trip.VehicleTypeId, _clock.GetUtcNow().UtcDateTime, cancellationToken)
                   ?? throw new BusinessRuleException("COMMISSION_NOT_CONFIGURED", "No commission rule is configured for this vehicle type.");
        var tds = await _settings.GetDecimalAsync(SettingKeys.SettlementTdsPercent, 1, cancellationToken);

        var created = await _settlements.CreateAsync(request.TripId, rule.CommissionPercent, rule.MinimumCommission, tds, _access.User.UserId,
            cancellationToken);
        await _audit.LogAsync("SettlementCreated", "Settlement", created.Id,
            newValue: new { created.Number, trip.TripNumber, rule.CommissionPercent, rule.MinimumCommission, TdsPercent = tds },
            cancellationToken: cancellationToken);
        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task AddAdjustmentAsync(long settlementId, SettlementAdjustmentRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);

        await _settlements.AddAdjustmentAsync(settlementId, request.Description.Trim(), request.Amount, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("SettlementAdjusted", "Settlement", settlementId,
            oldValue: new { settlement.NetAmount }, newValue: new { request.Description, request.Amount }, cancellationToken: cancellationToken);
    }

    public async Task ApproveAsync(long settlementId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ApproveSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);

        if (settlement.CreatedBy == _access.User.UserId)
        {
            throw new BusinessRuleException("SETTLEMENT_SELF_APPROVAL", "A settlement must be approved by someone other than its creator.");
        }

        await ChangeAsync(settlement, SettlementStatus.Approved, null, cancellationToken);
    }

    public async Task StartProcessingAsync(long settlementId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);
        await ChangeAsync(settlement, SettlementStatus.Processing, null, cancellationToken);
    }

    public async Task CompleteAsync(long settlementId, CompleteSettlementRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);
        StatusRules.Settlement.EnsureCanTransition((SettlementStatus)settlement.SettlementStatusId, SettlementStatus.Completed);

        await _settlements.CompleteAsync(settlementId, request.TransactionReference.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("SettlementCompleted", "Settlement", settlementId,
            newValue: new { request.TransactionReference, settlement.NetAmount }, cancellationToken: cancellationToken);

        await _notifications.NotifyUserAsync(settlement.OwnerUserId, NotificationTemplates.SettlementCompleted, new Dictionary<string, string>
        {
            ["Name"] = settlement.OwnerName,
            ["SettlementNumber"] = settlement.SettlementNumber,
            ["TripNumber"] = settlement.TripNumber,
            ["NetAmount"] = Formatting.Money(settlement.NetAmount),
            ["TransactionReference"] = request.TransactionReference.Trim()
        }, new NotificationSubject("Settlement", settlementId), cancellationToken);
    }

    public async Task FailAsync(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);
        await ChangeAsync(settlement, SettlementStatus.Failed, request.Reason.Trim(), cancellationToken);
    }

    public async Task CancelAsync(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSettlements);
        var settlement = await GetAccessibleAsync(settlementId, cancellationToken);
        await ChangeAsync(settlement, SettlementStatus.Cancelled, request.Reason.Trim(), cancellationToken);
    }

    public Task<OwnerEarningsSummaryDto> GetOwnerSummaryAsync(long? ownerId, CancellationToken cancellationToken)
    {
        long id;
        if (_access.IsStaffWith(Permissions.ViewFinance))
        {
            id = ownerId ?? throw new RequestValidationException(nameof(ownerId), "ownerId is required.");
        }
        else
        {
            id = _access.RequireOwnerId();
        }

        return _settlements.GetOwnerSummaryAsync(id, cancellationToken);
    }

    private async Task<SettlementDto> GetAccessibleAsync(long settlementId, CancellationToken cancellationToken)
    {
        var settlement = await _settlements.GetByIdAsync(settlementId, cancellationToken) ?? throw NotFoundException.For("Settlement", settlementId);
        _access.EnsureOwner(settlement.OwnerId, Permissions.ViewFinance, "Settlement", settlementId);
        return settlement;
    }

    private async Task ChangeAsync(SettlementDto settlement, SettlementStatus target, string? remarks, CancellationToken cancellationToken)
    {
        var current = (SettlementStatus)settlement.SettlementStatusId;
        StatusRules.Settlement.EnsureCanTransition(current, target);

        await _settlements.ChangeStatusAsync(settlement.SettlementId, current, target, remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("SettlementStatusChanged", "Settlement", settlement.SettlementId,
            oldValue: new { Status = current.ToString() }, newValue: new { Status = target.ToString(), Remarks = remarks },
            cancellationToken: cancellationToken);
    }

    private IReadOnlyList<string> AvailableActions(SettlementDto settlement)
    {
        var status = (SettlementStatus)settlement.SettlementStatusId;
        var actions = new List<string>();

        if (_access.IsStaffWith(Permissions.ApproveSettlements) && status == SettlementStatus.Pending
            && settlement.CreatedBy != _access.User.UserId)
        {
            actions.Add("Approve");
        }

        if (_access.IsStaffWith(Permissions.ManageSettlements))
        {
            if (status == SettlementStatus.Pending) actions.Add("Adjust");
            if (status == SettlementStatus.Approved) actions.Add("StartProcessing");
            if (status == SettlementStatus.Processing) actions.AddRange(["Complete", "Fail"]);
            if (status is SettlementStatus.Pending or SettlementStatus.Approved) actions.Add("Cancel");
        }

        return actions;
    }
}
