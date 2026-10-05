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
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Bookings;

/// <summary>
/// Booking use cases.
///
/// Security traceability
///   * Customer   - creates, edits (Draft/Submitted), submits and cancels (before pickup) OWN bookings.
///   * Owner/Driver - read-only access to bookings of trips assigned to them.
///   * Operations - ViewBookings to read all, ManageBookings to review / hold / resume / reject / cancel.
///   Enforced by: endpoint policies (controller) + AccessGuard ownership checks + StatusRules transitions here
///   + expected-status checks inside the stored procedures.
/// </summary>
public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookings;
    private readonly IMasterDataRepository _masterData;
    private readonly ISettingsProvider _settings;
    private readonly IDistanceEstimator _distance;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public BookingService(IBookingRepository bookings, IMasterDataRepository masterData, ISettingsProvider settings, IDistanceEstimator distance,
        INotificationService notifications, AccessGuard access, IAuditLogger audit, TimeProvider clock)
    {
        _bookings = bookings;
        _masterData = masterData;
        _settings = settings;
        _distance = distance;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public Task<PagedResult<BookingListItemDto>> GetPagedAsync(BookingSearchRequest request, CancellationToken cancellationToken)
    {
        var (customerId, ownerId, driverId) = _access.ScopeFilters(Permissions.ViewBookings, request.CustomerId);
        return _bookings.GetPagedAsync(request, new BookingScope(customerId, ownerId, driverId), cancellationToken);
    }

    public async Task<BookingDetailsResponse> GetByIdAsync(long bookingId, CancellationToken cancellationToken)
    {
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        var items = await _bookings.GetItemsAsync(bookingId, cancellationToken);
        return new BookingDetailsResponse(booking, items, AvailableActions(booking));
    }

    public async Task<BookingDetailsDto> GetAccessibleAsync(long bookingId, CancellationToken cancellationToken)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, cancellationToken) ?? throw NotFoundException.For("Booking", bookingId);
        _access.EnsureBooking(booking.BookingId, booking.CustomerId, booking.AssignedOwnerId, booking.AssignedDriverId);
        return booking;
    }

    public async Task<CreatedResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        long customerId;
        if (_access.IsStaffWith(Permissions.ManageBookings))
        {
            customerId = request.CustomerId ?? throw new RequestValidationException(nameof(request.CustomerId), "CustomerId is required.");
        }
        else
        {
            if (!_access.User.HasPermission(Permissions.CreateBookings)) throw new ForbiddenException();
            customerId = _access.RequireCustomerId();
        }

        var distance = await ValidateAsync(request, cancellationToken);

        var created = await _bookings.CreateAsync(ToCommand(null, customerId, request, distance, request.Submit, null), cancellationToken);

        await _audit.LogAsync("BookingCreated", "Booking", created.Id,
            newValue: new { created.Number, customerId, request.VehicleTypeId, Submitted = request.Submit }, cancellationToken: cancellationToken);

        if (request.Submit)
        {
            await NotifyCustomerAsync(created.Id, NotificationTemplates.BookingSubmitted, created.Number, null, cancellationToken);
        }

        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task UpdateAsync(long bookingId, UpdateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        EnsureCustomerOrStaff(booking, Permissions.ManageBookings);

        if (!StatusRules.IsBookingEditable((BookingStatus)booking.BookingStatusId))
        {
            throw new BusinessRuleException("BOOKING_NOT_EDITABLE", "Only draft or submitted bookings can be edited.");
        }

        var distance = await ValidateAsync(request, cancellationToken);
        await _bookings.UpdateAsync(ToCommand(bookingId, booking.CustomerId, request, distance, false, request.RowVersion), cancellationToken);
        await _audit.LogAsync("BookingUpdated", "Booking", bookingId, cancellationToken: cancellationToken);
    }

    public async Task SubmitAsync(long bookingId, CancellationToken cancellationToken)
    {
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        EnsureCustomerOrStaff(booking, Permissions.ManageBookings);
        await ChangeStatusAsync(booking, BookingStatus.Submitted, "Submitted for review", cancellationToken);
        await NotifyCustomerAsync(bookingId, NotificationTemplates.BookingSubmitted, booking.BookingNumber, null, cancellationToken);
    }

    public async Task ReviewAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageBookings);
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        await ChangeStatusAsync(booking, BookingStatus.UnderReview, request.Remarks ?? "Picked up for review", cancellationToken);
    }

    public async Task HoldAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageBookings);
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        await ChangeStatusAsync(booking, BookingStatus.OnHold, request.Remarks ?? "Put on hold", cancellationToken);
    }

    public async Task ResumeAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageBookings);
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);

        if (booking.BookingStatusId != (int)BookingStatus.OnHold || booking.StatusBeforeHoldId is null)
        {
            throw new BusinessRuleException("BOOKING_NOT_ON_HOLD", "The booking is not on hold.");
        }

        await ChangeStatusAsync(booking, (BookingStatus)booking.StatusBeforeHoldId.Value, request.Remarks ?? "Resumed", cancellationToken);
    }

    public async Task RejectAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageBookings);
        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            throw new RequestValidationException(nameof(request.Remarks), "A reason is required to reject a booking.");
        }

        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        await ChangeStatusAsync(booking, BookingStatus.Rejected, request.Remarks, cancellationToken);
        await NotifyCustomerAsync(bookingId, NotificationTemplates.BookingRejected, booking.BookingNumber, request.Remarks, cancellationToken);
    }

    public async Task CancelAsync(long bookingId, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        var status = (BookingStatus)booking.BookingStatusId;

        var isCustomer = _access.User.CustomerId == booking.CustomerId;
        var isStaff = _access.IsStaffWith(Permissions.ManageBookings);

        if (isCustomer && !isStaff && !StatusRules.IsBookingCancellableByCustomer(status))
        {
            throw new BusinessRuleException("BOOKING_NOT_CANCELLABLE",
                "This booking can no longer be cancelled online. Please contact support.");
        }

        if (!isCustomer && !isStaff)
        {
            throw NotFoundException.For("Booking", bookingId);
        }

        StatusRules.Booking.EnsureCanTransition(status, BookingStatus.Cancelled);

        if (status == BookingStatus.Assigned && booking.TripStatusId is not null && booking.TripStatusId != (int)TripStatus.Scheduled
            && booking.TripStatusId != (int)TripStatus.OnHold)
        {
            throw new BusinessRuleException("TRIP_ALREADY_STARTED", "The goods have been picked up; the booking cannot be cancelled.");
        }

        await _bookings.CancelAsync(bookingId, status, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("BookingCancelled", "Booking", bookingId, oldValue: new { Status = status.ToString() },
            newValue: new { request.Reason }, cancellationToken: cancellationToken);
        await NotifyCustomerAsync(bookingId, NotificationTemplates.BookingCancelled, booking.BookingNumber, request.Reason, cancellationToken);
    }

    public async Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long bookingId, CancellationToken cancellationToken)
    {
        await GetAccessibleAsync(bookingId, cancellationToken);
        return await _bookings.GetStatusHistoryAsync(bookingId, cancellationToken);
    }

    public async Task<IReadOnlyList<BookingNoteDto>> GetNotesAsync(long bookingId, CancellationToken cancellationToken)
    {
        await GetAccessibleAsync(bookingId, cancellationToken);
        return await _bookings.GetNotesAsync(bookingId, includeInternal: _access.User.IsStaff, cancellationToken);
    }

    public async Task<long> AddNoteAsync(long bookingId, AddBookingNoteRequest request, CancellationToken cancellationToken)
    {
        var booking = await GetAccessibleAsync(bookingId, cancellationToken);
        EnsureCustomerOrStaff(booking, Permissions.ViewBookings);

        var isInternal = _access.User.IsStaff && request.IsInternal;
        return await _bookings.AddNoteAsync(bookingId, request.Note.Trim(), isInternal, _access.User.UserId, cancellationToken);
    }

    private async Task ChangeStatusAsync(BookingDetailsDto booking, BookingStatus next, string? remarks, CancellationToken cancellationToken)
    {
        var current = (BookingStatus)booking.BookingStatusId;
        StatusRules.Booking.EnsureCanTransition(current, next);
        await _bookings.ChangeStatusAsync(booking.BookingId, current, next, remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("BookingStatusChanged", "Booking", booking.BookingId,
            oldValue: new { Status = current.ToString() }, newValue: new { Status = next.ToString(), remarks }, cancellationToken: cancellationToken);
    }

    /// <summary>Business validation shared by create and update. Returns the distance to store.</summary>
    private async Task<decimal?> ValidateAsync(BookingRequestBase request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var minLead = await _settings.GetIntAsync(SettingKeys.BookingMinLeadTimeMinutes, 60, cancellationToken);
        var maxAdvance = await _settings.GetIntAsync(SettingKeys.BookingMaxAdvanceDays, 90, cancellationToken);
        var maxItems = await _settings.GetIntAsync(SettingKeys.BookingMaxItems, 50, cancellationToken);

        var pickupUtc = DateTime.SpecifyKind(request.RequestedPickupDateUtc, DateTimeKind.Utc);
        if (pickupUtc < now.AddMinutes(minLead))
        {
            throw new BusinessRuleException("PICKUP_TOO_SOON", $"Pickup must be at least {minLead} minutes from now.");
        }

        if (pickupUtc > now.AddDays(maxAdvance))
        {
            throw new BusinessRuleException("PICKUP_TOO_FAR", $"Pickup can be scheduled at most {maxAdvance} days ahead.");
        }

        if (request.Items.Count > maxItems)
        {
            throw new BusinessRuleException("TOO_MANY_ITEMS", $"A booking can have at most {maxItems} items.");
        }

        var vehicleType = (await _masterData.GetVehicleTypesAsync(false, cancellationToken)).FirstOrDefault(v => v.VehicleTypeId == request.VehicleTypeId)
                          ?? throw new BusinessRuleException("VEHICLE_TYPE_INVALID", "The selected vehicle type is not available.");

        _ = (await _masterData.GetGoodsTypesAsync(false, cancellationToken)).FirstOrDefault(g => g.GoodsTypeId == request.GoodsTypeId)
            ?? throw new BusinessRuleException("GOODS_TYPE_INVALID", "The selected goods type is not available.");

        var totalWeight = request.Items.Sum(i => i.WeightKg);
        if (totalWeight > vehicleType.CapacityKg)
        {
            throw new BusinessRuleException("WEIGHT_EXCEEDS_CAPACITY",
                $"Total weight {totalWeight:0.##} kg exceeds the {vehicleType.Name} capacity of {vehicleType.CapacityKg:0} kg. Choose a larger vehicle.");
        }

        foreach (var cityId in new[] { request.PickupAddress.CityId, request.DeliveryAddress.CityId }.Distinct())
        {
            var city = await _masterData.GetCityAsync(cityId, cancellationToken);
            if (city is null || !city.IsActive)
            {
                throw new BusinessRuleException("CITY_NOT_SERVICED", "One of the selected cities is not serviced.");
            }
        }

        if (request.EstimatedDistanceKm is not null) return request.EstimatedDistanceKm;

        GeoPoint? from = request.PickupAddress.Latitude is { } plat && request.PickupAddress.Longitude is { } plon ? new GeoPoint(plat, plon) : null;
        GeoPoint? to = request.DeliveryAddress.Latitude is { } dlat && request.DeliveryAddress.Longitude is { } dlon ? new GeoPoint(dlat, dlon) : null;
        return _distance.EstimateRoadDistanceKm(from, to);
    }

    private SaveBookingCommand ToCommand(long? bookingId, long customerId, BookingRequestBase request, decimal? distance, bool submit, byte[]? rowVersion) =>
        new(bookingId, customerId, request, distance,
            IndianFormats.NormalizePhone(request.PickupContact.PhoneNumber),
            NormalizeOptional(request.PickupContact.AlternatePhoneNumber),
            IndianFormats.NormalizePhone(request.DeliveryContact.PhoneNumber),
            NormalizeOptional(request.DeliveryContact.AlternatePhoneNumber),
            submit, rowVersion, _access.User.UserId);

    private static string? NormalizeOptional(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : IndianFormats.NormalizePhone(phone);

    private void EnsureCustomerOrStaff(BookingDetailsDto booking, string staffPermission)
    {
        if (_access.User.CustomerId == booking.CustomerId) return;
        if (_access.IsStaffWith(staffPermission)) return;
        throw NotFoundException.For("Booking", booking.BookingId);
    }

    private IReadOnlyList<string> AvailableActions(BookingDetailsDto booking)
    {
        var status = (BookingStatus)booking.BookingStatusId;
        var isCustomer = _access.User.CustomerId == booking.CustomerId;
        var manage = _access.IsStaffWith(Permissions.ManageBookings);
        var actions = new List<string>();

        if (StatusRules.IsBookingEditable(status) && (isCustomer || manage)) actions.Add("Edit");
        if (status == BookingStatus.Draft && (isCustomer || manage)) actions.Add("Submit");
        if ((isCustomer && StatusRules.IsBookingCancellableByCustomer(status)) ||
            (manage && StatusRules.Booking.CanTransition(status, BookingStatus.Cancelled))) actions.Add("Cancel");
        if (manage && status == BookingStatus.Submitted) actions.Add("Review");
        if (manage && StatusRules.Booking.CanTransition(status, BookingStatus.OnHold)) actions.Add("Hold");
        if (manage && status == BookingStatus.OnHold) actions.Add("Resume");
        if (manage && StatusRules.Booking.CanTransition(status, BookingStatus.Rejected)) actions.Add("Reject");
        if (_access.IsStaffWith(Permissions.ManageQuotations) && status is BookingStatus.Submitted or BookingStatus.UnderReview or BookingStatus.Quoted)
            actions.Add("CreateQuotation");
        if (_access.IsStaffWith(Permissions.AssignTrips) && status == BookingStatus.Confirmed) actions.Add("AssignTrip");
        if (_access.IsStaffWith(Permissions.ManageInvoices) && status == BookingStatus.Delivered && booking.TripStatusId == (int)TripStatus.PodUploaded)
            actions.Add("GenerateInvoice");
        if (isCustomer || _access.User.IsStaff) actions.Add("AddNote");

        return actions;
    }

    private async Task NotifyCustomerAsync(long bookingId, string template, string bookingNumber, string? reason, CancellationToken cancellationToken)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) return;

        await _notifications.NotifyUserAsync(booking.CustomerUserId, template, new Dictionary<string, string>
        {
            ["BookingNumber"] = bookingNumber,
            ["Reason"] = reason ?? string.Empty
        }, new NotificationSubject("Booking", bookingId), cancellationToken);
    }
}
