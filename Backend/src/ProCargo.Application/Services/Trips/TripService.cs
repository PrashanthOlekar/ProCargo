using Microsoft.Extensions.Options;
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

namespace ProCargo.Application.Services.Trips;

/// <summary>
/// Trip execution: assignment, OTP-verified pickup and delivery, live tracking, proof of delivery.
///
/// Security traceability
///   * Operations (AssignTrips) create trips and reassign vehicles/drivers before pickup.
///   * Operations (UpdateTrips) hold / resume / cancel trips and resolve exceptions. Issuing the invoice
///     completes the trip (PodUploaded -> Completed) and settling the owner closes it - both in SQL.
///   * The ASSIGNED driver (PerformTrips) requests OTPs, verifies pickup/delivery, starts the trip, posts
///     locations and uploads proof of delivery - nobody else's trips (AccessGuard.EnsureCanOperateTrip).
///   * Customers, owners and drivers read only trips that involve them (404 otherwise).
///   * OTPs are random 6-digit codes sent by SMS to the pickup / delivery CONTACT (not to the driver), stored only
///     as an HMAC, limited in attempts and lifetime, and never logged.
///   * Every status change passes the expected current status to SQL, so concurrent changes fail with 409.
/// </summary>
public sealed class TripService : ITripService
{
    private const int MaxTrackingPoints = 500;

    private readonly ITripRepository _trips;
    private readonly IBookingService _bookings;
    private readonly IVehicleRepository _vehicles;
    private readonly IDriverRepository _drivers;
    private readonly IFileService _files;
    private readonly IOtpService _otp;
    private readonly ISettingsProvider _settings;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly SecurityOptions _security;

    public TripService(ITripRepository trips, IBookingService bookings, IVehicleRepository vehicles, IDriverRepository drivers,
        IFileService files, IOtpService otp, ISettingsProvider settings, INotificationService notifications, IUnitOfWork unitOfWork,
        AccessGuard access, IAuditLogger audit, TimeProvider clock, IOptions<SecurityOptions> security)
    {
        _trips = trips;
        _bookings = bookings;
        _vehicles = vehicles;
        _drivers = drivers;
        _files = files;
        _otp = otp;
        _settings = settings;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _access = access;
        _audit = audit;
        _clock = clock;
        _security = security.Value;
    }

    // ------------------------------------------------------------------ queries

    public Task<PagedResult<TripListItemDto>> GetPagedAsync(TripSearchRequest request, CancellationToken cancellationToken)
    {
        var (customerId, ownerId, driverId) = _access.ScopeFilters(Permissions.ViewTrips, request.CustomerId, request.OwnerId, request.DriverId);
        return _trips.GetPagedAsync(request, new TripScope(customerId, ownerId, driverId), cancellationToken);
    }

    public async Task<TripDetailsResponse> GetByIdAsync(long tripId, CancellationToken cancellationToken)
    {
        var trip = await GetAccessibleAsync(tripId, cancellationToken);
        return new TripDetailsResponse(trip, AvailableActions(trip));
    }

    public async Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long tripId, CancellationToken cancellationToken)
    {
        _ = await GetAccessibleAsync(tripId, cancellationToken);
        return await _trips.GetStatusHistoryAsync(tripId, cancellationToken);
    }

    public async Task<IReadOnlyList<TripAssignmentDto>> GetAssignmentsAsync(long tripId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewTrips);
        _ = await GetTripAsync(tripId, cancellationToken);
        return await _trips.GetAssignmentsAsync(tripId, cancellationToken);
    }

    // ------------------------------------------------------------------ assignment

    public async Task<CreatedResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.AssignTrips);

        var booking = await _bookings.GetAccessibleAsync(request.BookingId, cancellationToken);
        if (booking.BookingStatusId != (int)BookingStatus.Confirmed)
        {
            throw new BusinessRuleException("BOOKING_NOT_CONFIRMED", "A trip can only be created for a confirmed booking.");
        }

        var vehicle = await EnsureVehicleFitsAsync(request.VehicleId, booking.VehicleTypeId, booking.TotalWeightKg, cancellationToken);
        await EnsureDriverFitsAsync(request.DriverId, vehicle.OwnerId, cancellationToken);

        var created = await _trips.CreateAsync(request, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("TripCreated", "Trip", created.Id,
            newValue: new { created.Number, booking.BookingNumber, request.VehicleId, request.DriverId }, cancellationToken: cancellationToken);

        var trip = await GetTripAsync(created.Id, cancellationToken);
        await NotifyAssignmentAsync(trip, cancellationToken);

        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task ReassignVehicleAsync(long tripId, ReassignVehicleRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.AssignTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);
        EnsureBeforePickup(trip);

        var vehicle = await EnsureVehicleFitsAsync(request.VehicleId, trip.VehicleTypeId, trip.TotalWeightKg, cancellationToken);
        if (vehicle.OwnerId != trip.OwnerId)
        {
            // A different owner takes over: the current driver must be able to drive for the new owner.
            await EnsureDriverFitsAsync(trip.DriverId, vehicle.OwnerId, cancellationToken);
        }

        await _trips.AssignVehicleAsync(tripId, request.VehicleId, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("TripVehicleReassigned", "Trip", tripId,
            oldValue: new { trip.VehicleId, trip.VehicleNumber }, newValue: new { request.VehicleId, request.Reason },
            cancellationToken: cancellationToken);
    }

    public async Task ReassignDriverAsync(long tripId, ReassignDriverRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.AssignTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);
        EnsureBeforePickup(trip);
        await EnsureDriverFitsAsync(request.DriverId, trip.OwnerId, cancellationToken);

        await _trips.AssignDriverAsync(tripId, request.DriverId, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("TripDriverReassigned", "Trip", tripId,
            oldValue: new { trip.DriverId, trip.DriverName }, newValue: new { request.DriverId, request.Reason },
            cancellationToken: cancellationToken);

        var updated = await GetTripAsync(tripId, cancellationToken);
        if (updated.DriverUserId is long driverUserId)
        {
            await _notifications.NotifyUserAsync(driverUserId, NotificationTemplates.TripAssignedDriver, TripData(updated),
                new NotificationSubject("Trip", tripId), cancellationToken);
        }
    }

    // ------------------------------------------------------------------ OTP

    public async Task<OtpSentResponse> SendOtpAsync(long tripId, VerificationType type, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);

        var requiredStatus = type == VerificationType.Pickup ? TripStatus.Scheduled : TripStatus.InTransit;
        if (trip.TripStatusId != (int)requiredStatus)
        {
            throw new BusinessRuleException("OTP_NOT_ALLOWED",
                type == VerificationType.Pickup
                    ? "A pickup OTP can only be requested for a scheduled trip."
                    : "A delivery OTP can only be requested while the trip is in transit.");
        }

        var phone = type == VerificationType.Pickup ? trip.PickupContactPhone : trip.DeliveryContactPhone;
        var validityMinutes = await _settings.GetIntAsync(SettingKeys.OtpValidityMinutes, 10, cancellationToken);
        var maxAttempts = await _settings.GetIntAsync(SettingKeys.OtpMaxAttempts, 5, cancellationToken);
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(validityMinutes);

        var otp = _otp.GenerateOtp();
        await _trips.CreateVerificationAsync(new CreateVerificationCommand(
            tripId, type, _otp.HashOtp(tripId, type, otp), IndianFormats.MaskPhone(phone), expires, maxAttempts,
            _security.OtpResendSeconds, _access.User.UserId), cancellationToken);

        await _notifications.SendSmsAsync(phone,
            type == VerificationType.Pickup ? NotificationTemplates.PickupOtp : NotificationTemplates.DeliveryOtp,
            new Dictionary<string, string>
            {
                ["Otp"] = otp,
                ["TripNumber"] = trip.TripNumber,
                ["BookingNumber"] = trip.BookingNumber,
                ["ValidityMinutes"] = validityMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, cancellationToken);

        await _audit.LogAsync(type == VerificationType.Pickup ? "PickupOtpSent" : "DeliveryOtpSent", "Trip", tripId,
            newValue: new { SentTo = IndianFormats.MaskPhone(phone) }, cancellationToken: cancellationToken);

        return new OtpSentResponse(IndianFormats.MaskPhone(phone), expires)
        {
            TestOtp = _security.ExposeOtpForTesting ? otp : null
        };
    }

    public async Task VerifyOtpAsync(long tripId, VerificationType type, VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);

        var verification = await _trips.GetActiveVerificationAsync(tripId, type, cancellationToken)
                           ?? throw new BusinessRuleException("OTP_NOT_REQUESTED", "Request an OTP first.");

        if (verification.IsVerified)
        {
            throw new BusinessRuleException("ALREADY_VERIFIED", "This step has already been verified.");
        }

        if (verification.ExpiresDateUtc <= _clock.GetUtcNow().UtcDateTime || verification.AttemptCount >= verification.MaxAttempts)
        {
            throw new BusinessRuleException("OTP_EXPIRED", "The OTP has expired or too many attempts were made. Request a new OTP.");
        }

        var latitude = request.Latitude;
        var longitude = request.Longitude;
        var receiver = request.ReceiverName?.Trim();

        if (!_otp.Verify(tripId, type, request.Otp, verification.OtpHash))
        {
            var attempts = await _trips.RegisterVerificationAttemptAsync(
                new VerificationAttempt(verification.TripVerificationId, false, _access.User.UserId, null, null, null), cancellationToken);
            await _audit.LogAsync(type == VerificationType.Pickup ? "PickupOtpFailed" : "DeliveryOtpFailed", "Trip", tripId,
                newValue: new { Attempt = attempts }, cancellationToken: cancellationToken);

            var remaining = Math.Max(0, verification.MaxAttempts - attempts);
            throw new BusinessRuleException(ErrorCodes.OtpInvalid,
                remaining > 0 ? $"Incorrect OTP. {remaining} attempt(s) left." : "Incorrect OTP. Request a new OTP.");
        }

        var change = type == VerificationType.Pickup
            ? new TripStatusChange(tripId, TripStatus.Scheduled, TripStatus.PickupVerified, "Pickup OTP verified", _access.User.UserId,
                request.Odometer)
            : new TripStatusChange(tripId, TripStatus.InTransit, TripStatus.Delivered, "Delivery OTP verified", _access.User.UserId,
                request.Odometer, BookingStatus.InTransit, BookingStatus.Delivered);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _trips.RegisterVerificationAttemptAsync(
                new VerificationAttempt(verification.TripVerificationId, true, _access.User.UserId, receiver, latitude, longitude), ct);
            await _trips.UpdateStatusAsync(change, ct);
            return true;
        }, cancellationToken);

        await _audit.LogAsync(type == VerificationType.Pickup ? "PickupVerified" : "DeliveryVerified", "Trip", tripId,
            newValue: new { request.Odometer, ReceiverName = receiver }, cancellationToken: cancellationToken);

        if (type == VerificationType.Delivery)
        {
            await _notifications.NotifyUserAsync(trip.CustomerUserId, NotificationTemplates.TripDelivered, TripData(trip),
                new NotificationSubject("Trip", tripId), cancellationToken);
        }
    }

    // ------------------------------------------------------------------ status changes

    public async Task StartAsync(long tripId, StartTripRequest request, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);

        await ChangeStatusAsync(trip, TripStatus.InTransit, request.Remarks?.Trim() ?? "Trip started", null,
            BookingStatus.Assigned, BookingStatus.InTransit, cancellationToken);

        await _notifications.NotifyUserAsync(trip.CustomerUserId, NotificationTemplates.TripStarted, TripData(trip),
            new NotificationSubject("Trip", tripId), cancellationToken);
    }

    public async Task HoldAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.UpdateTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);
        await ChangeStatusAsync(trip, TripStatus.OnHold, request.Reason.Trim(), null, null, null, cancellationToken);
    }

    public async Task ResumeAsync(long tripId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.UpdateTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);
        if (trip.TripStatusId != (int)TripStatus.OnHold)
        {
            throw new BusinessRuleException("TRIP_NOT_ON_HOLD", "Only trips on hold can be resumed.");
        }

        var target = (TripStatus)(trip.StatusBeforeHoldId ?? (int)TripStatus.Scheduled);
        await ChangeStatusAsync(trip, target, "Resumed", null, null, null, cancellationToken);
    }

    public async Task ReportExceptionAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);
        await ChangeStatusAsync(trip, TripStatus.Exception, request.Reason.Trim(), null, null, null, cancellationToken);
    }

    public async Task ResolveExceptionAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.UpdateTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);
        if (trip.TripStatusId != (int)TripStatus.Exception)
        {
            throw new BusinessRuleException("TRIP_NOT_IN_EXCEPTION", "The trip has no open exception.");
        }

        // The booking only moves to InTransit when the driver starts the trip, so it tells us where to return.
        var target = trip.BookingStatusId == (int)BookingStatus.InTransit ? TripStatus.InTransit : TripStatus.PickupVerified;
        await ChangeStatusAsync(trip, target, request.Reason.Trim(), null, null, null, cancellationToken);
    }

    public async Task CancelAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.UpdateTrips);
        var trip = await GetTripAsync(tripId, cancellationToken);

        var beforePickup = trip.TripStatusId == (int)TripStatus.Scheduled
                           || (trip.TripStatusId == (int)TripStatus.OnHold && trip.StatusBeforeHoldId == (int)TripStatus.Scheduled);
        if (!beforePickup)
        {
            throw new BusinessRuleException("TRIP_NOT_CANCELLABLE",
                "Trips can only be cancelled before pickup. Use hold or exception handling for trips under way.");
        }

        // The booking returns to Confirmed so operations can assign another vehicle.
        await ChangeStatusAsync(trip, TripStatus.Cancelled, request.Reason.Trim(), null,
            BookingStatus.Assigned, BookingStatus.Confirmed, cancellationToken);
    }

    // ------------------------------------------------------------------ tracking

    public async Task<int> AddLocationsAsync(long tripId, AddLocationsRequest request, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);

        if (!StatusRules.IsTripTrackable((TripStatus)trip.TripStatusId))
        {
            throw new BusinessRuleException("TRIP_NOT_TRACKABLE", "Locations are accepted only between pickup and delivery.");
        }

        var provider = _access.User.DriverId == trip.DriverId ? TrackingProviderType.DriverApp : TrackingProviderType.Manual;
        return await _trips.AddLocationsAsync(tripId, provider, request.Points, cancellationToken);
    }

    public async Task<TripTrackingResponse> GetTrackingAsync(long tripId, DateTime? sinceUtc, CancellationToken cancellationToken)
    {
        var trip = await GetAccessibleAsync(tripId, cancellationToken);
        var points = await _trips.GetLocationsAsync(tripId, sinceUtc, MaxTrackingPoints, cancellationToken);
        return new TripTrackingResponse(tripId, trip.TripStatusId, points.Count > 0 ? points[^1] : null, points);
    }

    // ------------------------------------------------------------------ proof of delivery

    public async Task<long> UploadProofOfDeliveryAsync(long tripId, UploadProofOfDeliveryRequest request, IReadOnlyList<FileUpload> photos,
        FileUpload? signature, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureCanOperateTrip(tripId, trip.DriverId);

        if (trip.TripStatusId != (int)TripStatus.Delivered)
        {
            throw new BusinessRuleException("TRIP_NOT_DELIVERED", "Proof of delivery can only be uploaded after delivery is verified.");
        }

        if (photos.Count == 0 && signature is null)
        {
            throw new RequestValidationException("files", "Attach at least one delivery photo or the receiver's signature.");
        }

        if (photos.Count > 5)
        {
            throw new RequestValidationException("files", "Attach at most 5 photos.");
        }

        var phone = string.IsNullOrWhiteSpace(request.ReceiverPhone) ? null : IndianFormats.NormalizePhone(request.ReceiverPhone);

        var podId = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var files = new List<(long, string)>();
            foreach (var photo in photos)
            {
                files.Add((await _files.StoreAsync(photo, "pod", ct), "Photo"));
            }

            long? signatureId = signature is null ? null : await _files.StoreAsync(signature, "pod", ct);

            return await _trips.CreateProofOfDeliveryAsync(new CreateProofOfDeliveryCommand(
                tripId, request.ReceiverName.Trim(), phone, request.Remarks?.Trim(), request.Latitude, request.Longitude,
                signatureId, files, _access.User.UserId), ct);
        }, cancellationToken);

        await _audit.LogAsync("ProofOfDeliveryUploaded", "Trip", tripId,
            newValue: new { ProofOfDeliveryId = podId, Photos = photos.Count, HasSignature = signature is not null },
            cancellationToken: cancellationToken);
        return podId;
    }

    public async Task<ProofOfDeliveryResponse> GetProofOfDeliveryAsync(long tripId, CancellationToken cancellationToken)
    {
        _ = await GetAccessibleAsync(tripId, cancellationToken);
        var pod = await _trips.GetProofOfDeliveryAsync(tripId, cancellationToken) ?? throw NotFoundException.For("ProofOfDelivery", tripId);
        var files = await _trips.GetProofOfDeliveryFilesAsync(tripId, cancellationToken);
        return new ProofOfDeliveryResponse(pod, files);
    }

    public async Task<FileDownload> DownloadProofOfDeliveryFileAsync(long tripId, long storedFileId, CancellationToken cancellationToken)
    {
        _ = await GetAccessibleAsync(tripId, cancellationToken);
        var pod = await _trips.GetProofOfDeliveryAsync(tripId, cancellationToken) ?? throw NotFoundException.For("File", storedFileId);
        var files = await _trips.GetProofOfDeliveryFilesAsync(tripId, cancellationToken);

        var belongsToTrip = pod.SignatureFileId == storedFileId || files.Any(f => f.StoredFileId == storedFileId);
        if (!belongsToTrip) throw NotFoundException.For("File", storedFileId);

        return await _files.OpenAsync(storedFileId, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<TripDto> GetTripAsync(long tripId, CancellationToken cancellationToken) =>
        await _trips.GetByIdAsync(tripId, cancellationToken) ?? throw NotFoundException.For("Trip", tripId);

    private async Task<TripDto> GetAccessibleAsync(long tripId, CancellationToken cancellationToken)
    {
        var trip = await GetTripAsync(tripId, cancellationToken);
        _access.EnsureTrip(tripId, trip.CustomerId, trip.OwnerId, trip.DriverId);
        return trip;
    }

    private async Task ChangeStatusAsync(TripDto trip, TripStatus target, string remarks, int? odometer,
        BookingStatus? bookingExpected, BookingStatus? bookingNew, CancellationToken cancellationToken)
    {
        var current = (TripStatus)trip.TripStatusId;
        StatusRules.Trip.EnsureCanTransition(current, target);

        // Only move the booking when it is where this transition expects it.
        if (bookingExpected is not null && trip.BookingStatusId != (int)bookingExpected)
        {
            bookingExpected = null;
            bookingNew = null;
        }

        await _trips.UpdateStatusAsync(new TripStatusChange(trip.TripId, current, target, remarks, _access.User.UserId, odometer,
            bookingExpected, bookingNew), cancellationToken);

        await _audit.LogAsync("TripStatusChanged", "Trip", trip.TripId,
            oldValue: new { Status = current.ToString() }, newValue: new { Status = target.ToString(), Remarks = remarks },
            cancellationToken: cancellationToken);
    }

    private static void EnsureBeforePickup(TripDto trip)
    {
        if ((TripStatus)trip.TripStatusId is not (TripStatus.Scheduled or TripStatus.OnHold) || trip.ActualPickupDateUtc is not null)
        {
            throw new BusinessRuleException("TRIP_NOT_REASSIGNABLE", "Vehicles and drivers can only be changed before pickup.");
        }
    }

    private async Task<VehicleDto> EnsureVehicleFitsAsync(long vehicleId, int vehicleTypeId, decimal weightKg, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken) ?? throw NotFoundException.For("Vehicle", vehicleId);

        if (!vehicle.IsActive || vehicle.VerificationStatusId != (int)VerificationStatus.Verified)
        {
            throw new BusinessRuleException("VEHICLE_NOT_VERIFIED", "Only active, verified vehicles can be assigned.");
        }

        if (vehicle.VehicleTypeId != vehicleTypeId)
        {
            throw new BusinessRuleException("VEHICLE_TYPE_MISMATCH", "The vehicle type does not match the booking.");
        }

        if (vehicle.CapacityKg < weightKg)
        {
            throw new BusinessRuleException("VEHICLE_CAPACITY_EXCEEDED", "The vehicle cannot carry the booked weight.");
        }

        return vehicle;
    }

    private async Task EnsureDriverFitsAsync(long driverId, long vehicleOwnerId, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken) ?? throw NotFoundException.For("Driver", driverId);

        if (!driver.IsActive || driver.VerificationStatusId != (int)VerificationStatus.Verified)
        {
            throw new BusinessRuleException("DRIVER_NOT_VERIFIED", "Only active, verified drivers can be assigned.");
        }

        if (driver.OwnerId is not null && driver.OwnerId != vehicleOwnerId)
        {
            throw new BusinessRuleException("DRIVER_OWNER_MISMATCH", "The driver works for a different vehicle owner.");
        }
    }

    private async Task NotifyAssignmentAsync(TripDto trip, CancellationToken cancellationToken)
    {
        var data = TripData(trip);
        var subject = new NotificationSubject("Trip", trip.TripId);
        await _notifications.NotifyUserAsync(trip.CustomerUserId, NotificationTemplates.TripAssigned, data, subject, cancellationToken);
        await _notifications.NotifyUserAsync(trip.OwnerUserId, NotificationTemplates.TripAssignedOwner, data, subject, cancellationToken);
        if (trip.DriverUserId is long driverUserId)
        {
            await _notifications.NotifyUserAsync(driverUserId, NotificationTemplates.TripAssignedDriver, data, subject, cancellationToken);
        }
    }

    private static Dictionary<string, string> TripData(TripDto trip) => new()
    {
        ["TripNumber"] = trip.TripNumber,
        ["BookingNumber"] = trip.BookingNumber,
        ["CustomerName"] = trip.CustomerName,
        ["VehicleNumber"] = trip.VehicleNumber,
        ["DriverName"] = trip.DriverName,
        ["PickupCity"] = trip.PickupCityName,
        ["DeliveryCity"] = trip.DeliveryCityName,
        ["PickupDate"] = Formatting.ToIst(trip.PlannedPickupDateUtc),
        ["PickupTime"] = Formatting.ToIst(trip.PlannedPickupDateUtc)
    };

    private IReadOnlyList<string> AvailableActions(TripDto trip)
    {
        var status = (TripStatus)trip.TripStatusId;
        var actions = new List<string>();
        var isDriver = _access.User.DriverId == trip.DriverId && _access.User.HasPermission(Permissions.PerformTrips);
        var ops = _access.IsStaffWith(Permissions.UpdateTrips);
        var assign = _access.IsStaffWith(Permissions.AssignTrips);

        if (isDriver || ops)
        {
            if (status == TripStatus.Scheduled) actions.AddRange(["SendPickupOtp", "VerifyPickup"]);
            if (status == TripStatus.PickupVerified) actions.Add("Start");
            if (status == TripStatus.InTransit) actions.AddRange(["SendDeliveryOtp", "VerifyDelivery"]);
            if (status == TripStatus.Delivered) actions.Add("UploadProofOfDelivery");
            if (StatusRules.IsTripTrackable(status)) actions.Add("PostLocation");
            if (status is TripStatus.PickupVerified or TripStatus.InTransit) actions.Add("ReportException");
        }

        if (assign && status is TripStatus.Scheduled or TripStatus.OnHold && trip.ActualPickupDateUtc is null)
        {
            actions.AddRange(["ReassignVehicle", "ReassignDriver"]);
        }

        if (ops)
        {
            if (StatusRules.Trip.CanTransition(status, TripStatus.OnHold)) actions.Add("Hold");
            if (status == TripStatus.OnHold) actions.Add("Resume");
            if (status == TripStatus.Exception) actions.Add("ResolveException");
            if (status == TripStatus.Scheduled || (status == TripStatus.OnHold && trip.StatusBeforeHoldId == (int)TripStatus.Scheduled))
            {
                actions.Add("Cancel");
            }
        }

        return actions.Distinct().ToList();
    }
}
