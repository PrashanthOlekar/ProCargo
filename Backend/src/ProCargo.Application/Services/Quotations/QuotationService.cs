using System.Globalization;
using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Application.Services.Pricing;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;
using ProCargo.Domain.Pricing;

namespace ProCargo.Application.Services.Quotations;

/// <summary>
/// Quotation workflow: Operations price a booking (draft), send it, the customer accepts or rejects it.
///
/// Rules
///   * Only bookings that are Submitted / UnderReview / Quoted can be quoted.
///   * A discount above the configured % of the pre-discount amount needs the ApproveQuotations permission
///     (maker/checker for revenue leakage).
///   * Sending a new quotation supersedes the previous open one; accepting confirms the booking.
///   * Customers never see draft quotations.
/// </summary>
public sealed class QuotationService : IQuotationService
{
    private readonly IQuotationRepository _quotations;
    private readonly IBookingService _bookings;
    private readonly IPricingService _pricing;
    private readonly ISettingsProvider _settings;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public QuotationService(IQuotationRepository quotations, IBookingService bookings, IPricingService pricing, ISettingsProvider settings,
        INotificationService notifications, AccessGuard access, IAuditLogger audit, TimeProvider clock)
    {
        _quotations = quotations;
        _bookings = bookings;
        _pricing = pricing;
        _settings = settings;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationSearchRequest request, CancellationToken cancellationToken)
    {
        if (_access.IsStaffWith(Permissions.ViewBookings) || _access.IsStaffWith(Permissions.ManageQuotations))
        {
            return _quotations.GetPagedAsync(request, request.CustomerId, excludeDrafts: false, cancellationToken);
        }

        return _quotations.GetPagedAsync(request, _access.RequireCustomerId(), excludeDrafts: true, cancellationToken);
    }

    public async Task<QuotationDetailsResponse> GetByIdAsync(long quotationId, CancellationToken cancellationToken)
    {
        var quotation = await GetAccessibleAsync(quotationId, cancellationToken);
        var charges = await _quotations.GetChargesAsync(quotationId, cancellationToken);
        return new QuotationDetailsResponse(quotation, charges, AvailableActions(quotation));
    }

    public async Task<PriceEstimateResponse> PreviewAsync(PreviewQuotationRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageQuotations);
        var booking = await _bookings.GetAccessibleAsync(request.BookingId, cancellationToken);
        var price = await _pricing.PriceBookingAsync(booking, request, cancellationToken);
        return PricingService.ToResponse(price);
    }

    public async Task<CreatedResponse> CreateAsync(CreateQuotationRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageQuotations);
        var booking = await _bookings.GetAccessibleAsync(request.BookingId, cancellationToken);

        if ((BookingStatus)booking.BookingStatusId is not (BookingStatus.Submitted or BookingStatus.UnderReview or BookingStatus.Quoted))
        {
            throw new BusinessRuleException("BOOKING_NOT_QUOTABLE", "Quotations can only be prepared for bookings awaiting a price.");
        }

        var price = await _pricing.PriceBookingAsync(booking, request, cancellationToken);
        await EnsureDiscountAllowedAsync(price, cancellationToken);

        var created = await _quotations.CreateAsync(new SaveQuotationCommand(
            null, booking.BookingId, price, await ValidityAsync(request.ValidityHours, cancellationToken), request.Notes?.Trim(), null,
            _access.User.UserId), cancellationToken);

        await _audit.LogAsync("QuotationCreated", "Quotation", created.Id,
            newValue: new { created.Number, booking.BookingNumber, price.SubTotal, price.DiscountAmount, price.TotalAmount },
            cancellationToken: cancellationToken);

        if (request.SendImmediately)
        {
            await SendAsync(created.Id, cancellationToken);
        }

        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task UpdateAsync(long quotationId, UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageQuotations);
        var quotation = await GetAccessibleAsync(quotationId, cancellationToken);

        if (quotation.QuotationStatusId != (int)QuotationStatus.Draft)
        {
            throw new BusinessRuleException("QUOTATION_NOT_EDITABLE", "Only draft quotations can be edited.");
        }

        var booking = await _bookings.GetAccessibleAsync(quotation.BookingId, cancellationToken);
        var price = await _pricing.PriceBookingAsync(booking, request, cancellationToken);
        await EnsureDiscountAllowedAsync(price, cancellationToken);

        await _quotations.UpdateAsync(new SaveQuotationCommand(
            quotationId, quotation.BookingId, price, await ValidityAsync(request.ValidityHours, cancellationToken), request.Notes?.Trim(),
            request.RowVersion, _access.User.UserId), cancellationToken);

        await _audit.LogAsync("QuotationUpdated", "Quotation", quotationId,
            oldValue: new { quotation.TotalAmount, quotation.DiscountAmount },
            newValue: new { price.TotalAmount, price.DiscountAmount }, cancellationToken: cancellationToken);
    }

    public async Task SendAsync(long quotationId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageQuotations);
        var quotation = await GetAccessibleAsync(quotationId, cancellationToken);

        await _quotations.SendAsync(quotationId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("QuotationSent", "Quotation", quotationId, cancellationToken: cancellationToken);

        await _notifications.NotifyUserAsync(quotation.CustomerUserId, NotificationTemplates.QuotationSent, new Dictionary<string, string>
        {
            ["Name"] = quotation.CustomerName,
            ["QuotationNumber"] = quotation.QuotationNumber,
            ["BookingNumber"] = quotation.BookingNumber,
            ["TotalAmount"] = quotation.TotalAmount.ToString("N2", CultureInfo.GetCultureInfo("en-IN")),
            ["ValidUntil"] = Formatting.ToIst(quotation.ValidityDateUtc)
        }, new NotificationSubject("Quotation", quotationId), cancellationToken);
    }

    public async Task AcceptAsync(long quotationId, CancellationToken cancellationToken)
    {
        var customerId = RequireResponder();
        var quotation = await GetAccessibleAsync(quotationId, cancellationToken);

        await _quotations.AcceptAsync(quotationId, customerId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("QuotationAccepted", "Quotation", quotationId,
            newValue: new { quotation.QuotationNumber, quotation.TotalAmount }, cancellationToken: cancellationToken);
    }

    public async Task RejectAsync(long quotationId, RejectQuotationRequest request, CancellationToken cancellationToken)
    {
        var customerId = RequireResponder();
        _ = await GetAccessibleAsync(quotationId, cancellationToken);

        await _quotations.RejectAsync(quotationId, customerId, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("QuotationRejected", "Quotation", quotationId, newValue: new { request.Reason }, cancellationToken: cancellationToken);
    }

    public async Task WithdrawAsync(long quotationId, WithdrawQuotationRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageQuotations);
        _ = await GetAccessibleAsync(quotationId, cancellationToken);
        await _quotations.WithdrawAsync(quotationId, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("QuotationWithdrawn", "Quotation", quotationId, newValue: new { request.Reason }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long quotationId, CancellationToken cancellationToken)
    {
        _ = await GetAccessibleAsync(quotationId, cancellationToken);
        return await _quotations.GetStatusHistoryAsync(quotationId, cancellationToken);
    }

    public Task<int> ExpireOverdueAsync(CancellationToken cancellationToken) =>
        _quotations.ExpireOverdueAsync(_clock.GetUtcNow().UtcDateTime, cancellationToken);

    private async Task<QuotationDto> GetAccessibleAsync(long quotationId, CancellationToken cancellationToken)
    {
        var quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken) ?? throw NotFoundException.For("Quotation", quotationId);

        if (_access.IsStaffWith(Permissions.ViewBookings) || _access.IsStaffWith(Permissions.ManageQuotations)) return quotation;

        var isOwnCustomer = _access.User.CustomerId == quotation.CustomerId;
        if (isOwnCustomer && quotation.QuotationStatusId != (int)QuotationStatus.Draft) return quotation;

        throw NotFoundException.For("Quotation", quotationId);
    }

    private long RequireResponder()
    {
        if (!_access.User.HasPermission(Permissions.RespondToQuotations)) throw new ForbiddenException();
        return _access.RequireCustomerId();
    }

    private async Task EnsureDiscountAllowedAsync(PriceBreakdown price, CancellationToken cancellationToken)
    {
        if (price.DiscountAmount <= 0) return;

        var threshold = await _settings.GetDecimalAsync(SettingKeys.QuotationDiscountApprovalThresholdPercent, 10, cancellationToken);
        var beforeDiscount = price.SubTotal + price.DiscountAmount;
        var percent = beforeDiscount == 0 ? 100 : price.DiscountAmount * 100 / beforeDiscount;

        if (percent > threshold && !_access.IsStaffWith(Permissions.ApproveQuotations))
        {
            throw new BusinessRuleException(ErrorCodes.DiscountApprovalRequired,
                $"Discounts above {threshold:0.##}% need approval by a user with the ApproveQuotations permission.");
        }
    }

    private async Task<DateTime> ValidityAsync(int? hours, CancellationToken cancellationToken)
    {
        var validity = hours ?? await _settings.GetIntAsync(SettingKeys.QuotationDefaultValidityHours, 48, cancellationToken);
        return _clock.GetUtcNow().UtcDateTime.AddHours(validity);
    }

    private IReadOnlyList<string> AvailableActions(QuotationDto quotation)
    {
        var status = (QuotationStatus)quotation.QuotationStatusId;
        var actions = new List<string>();
        var manage = _access.IsStaffWith(Permissions.ManageQuotations);
        var isCustomer = _access.User.CustomerId == quotation.CustomerId && _access.User.HasPermission(Permissions.RespondToQuotations);
        var valid = quotation.ValidityDateUtc > _clock.GetUtcNow().UtcDateTime;

        if (manage && status == QuotationStatus.Draft) actions.AddRange(["Edit", "Send"]);
        if (manage && status is QuotationStatus.Draft or QuotationStatus.Sent) actions.Add("Withdraw");
        if (isCustomer && status == QuotationStatus.Sent && valid) actions.AddRange(["Accept", "Reject"]);
        return actions;
    }
}
