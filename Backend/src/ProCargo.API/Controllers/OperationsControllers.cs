using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProCargo.API.Authorization;
using ProCargo.API.Extensions;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.API.Controllers;

/// <summary>
/// Bookings. Customers create and manage their own; owners and drivers read bookings of trips assigned to them;
/// Operations review, hold, reject and cancel. Ownership is enforced in the service (404 for other people's ids).
/// </summary>
[Route("api/v1/bookings")]
[Authorize]
public sealed class BookingsController : ApiControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public Task<PagedResult<BookingListItemDto>> Get([FromQuery] BookingSearchRequest request, CancellationToken cancellationToken) =>
        _bookings.GetPagedAsync(request, cancellationToken);

    [HttpGet("{bookingId:long}")]
    public Task<BookingDetailsResponse> GetById(long bookingId, CancellationToken cancellationToken) => _bookings.GetByIdAsync(bookingId, cancellationToken);

    /// <summary>Creates a booking as a draft, or submits it immediately when <c>submit</c> is true.</summary>
    [HttpPost]
    [HasAnyPermission(Permissions.CreateBookings, Permissions.ManageBookings)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var created = await _bookings.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { bookingId = created.Id }, created);
    }

    [HttpPut("{bookingId:long}")]
    [HasAnyPermission(Permissions.CreateBookings, Permissions.ManageBookings)]
    public async Task<IActionResult> Update(long bookingId, UpdateBookingRequest request, CancellationToken cancellationToken)
    {
        await _bookings.UpdateAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/submit")]
    [HasAnyPermission(Permissions.CreateBookings, Permissions.ManageBookings)]
    public async Task<IActionResult> Submit(long bookingId, CancellationToken cancellationToken)
    {
        await _bookings.SubmitAsync(bookingId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/review")]
    [HasPermission(Permissions.ManageBookings)]
    public async Task<IActionResult> Review(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        await _bookings.ReviewAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/hold")]
    [HasPermission(Permissions.ManageBookings)]
    public async Task<IActionResult> Hold(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        await _bookings.HoldAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/resume")]
    [HasPermission(Permissions.ManageBookings)]
    public async Task<IActionResult> Resume(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        await _bookings.ResumeAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/reject")]
    [HasPermission(Permissions.ManageBookings)]
    public async Task<IActionResult> Reject(long bookingId, BookingActionRequest request, CancellationToken cancellationToken)
    {
        await _bookings.RejectAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{bookingId:long}/cancel")]
    [HasAnyPermission(Permissions.CreateBookings, Permissions.ManageBookings)]
    public async Task<IActionResult> Cancel(long bookingId, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        await _bookings.CancelAsync(bookingId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{bookingId:long}/history")]
    public Task<IReadOnlyList<StatusHistoryDto>> GetHistory(long bookingId, CancellationToken cancellationToken) =>
        _bookings.GetHistoryAsync(bookingId, cancellationToken);

    [HttpGet("{bookingId:long}/notes")]
    public Task<IReadOnlyList<BookingNoteDto>> GetNotes(long bookingId, CancellationToken cancellationToken) =>
        _bookings.GetNotesAsync(bookingId, cancellationToken);

    [HttpPost("{bookingId:long}/notes")]
    public async Task<CreatedResponse> AddNote(long bookingId, AddBookingNoteRequest request, CancellationToken cancellationToken) =>
        new(await _bookings.AddNoteAsync(bookingId, request, cancellationToken), null);
}

/// <summary>Quotations: Operations price and send; customers accept or reject.</summary>
[Route("api/v1/quotations")]
[Authorize]
public sealed class QuotationsController : ApiControllerBase
{
    private readonly IQuotationService _quotations;

    public QuotationsController(IQuotationService quotations)
    {
        _quotations = quotations;
    }

    [HttpGet]
    public Task<PagedResult<QuotationListItemDto>> Get([FromQuery] QuotationSearchRequest request, CancellationToken cancellationToken) =>
        _quotations.GetPagedAsync(request, cancellationToken);

    [HttpGet("{quotationId:long}")]
    public Task<QuotationDetailsResponse> GetById(long quotationId, CancellationToken cancellationToken) =>
        _quotations.GetByIdAsync(quotationId, cancellationToken);

    /// <summary>Calculates the price for a booking without saving anything.</summary>
    [HttpPost("preview")]
    [HasPermission(Permissions.ManageQuotations)]
    public Task<PriceEstimateResponse> Preview(PreviewQuotationRequest request, CancellationToken cancellationToken) =>
        _quotations.PreviewAsync(request, cancellationToken);

    [HttpPost]
    [HasPermission(Permissions.ManageQuotations)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateQuotationRequest request, CancellationToken cancellationToken)
    {
        var created = await _quotations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { quotationId = created.Id }, created);
    }

    [HttpPut("{quotationId:long}")]
    [HasPermission(Permissions.ManageQuotations)]
    public async Task<IActionResult> Update(long quotationId, UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        await _quotations.UpdateAsync(quotationId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{quotationId:long}/send")]
    [HasPermission(Permissions.ManageQuotations)]
    public async Task<IActionResult> Send(long quotationId, CancellationToken cancellationToken)
    {
        await _quotations.SendAsync(quotationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{quotationId:long}/withdraw")]
    [HasPermission(Permissions.ManageQuotations)]
    public async Task<IActionResult> Withdraw(long quotationId, WithdrawQuotationRequest request, CancellationToken cancellationToken)
    {
        await _quotations.WithdrawAsync(quotationId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{quotationId:long}/accept")]
    [HasPermission(Permissions.RespondToQuotations)]
    public async Task<IActionResult> Accept(long quotationId, CancellationToken cancellationToken)
    {
        await _quotations.AcceptAsync(quotationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{quotationId:long}/reject")]
    [HasPermission(Permissions.RespondToQuotations)]
    public async Task<IActionResult> Reject(long quotationId, RejectQuotationRequest request, CancellationToken cancellationToken)
    {
        await _quotations.RejectAsync(quotationId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{quotationId:long}/history")]
    public Task<IReadOnlyList<StatusHistoryDto>> GetHistory(long quotationId, CancellationToken cancellationToken) =>
        _quotations.GetHistoryAsync(quotationId, cancellationToken);
}

/// <summary>Trips: assignment (Operations), OTP pickup/delivery, tracking and proof of delivery (assigned driver).</summary>
[Route("api/v1/trips")]
[Authorize]
public sealed class TripsController : ApiControllerBase
{
    private readonly ITripService _trips;

    public TripsController(ITripService trips)
    {
        _trips = trips;
    }

    [HttpGet]
    public Task<PagedResult<TripListItemDto>> Get([FromQuery] TripSearchRequest request, CancellationToken cancellationToken) =>
        _trips.GetPagedAsync(request, cancellationToken);

    [HttpGet("{tripId:long}")]
    public Task<TripDetailsResponse> GetById(long tripId, CancellationToken cancellationToken) => _trips.GetByIdAsync(tripId, cancellationToken);

    [HttpPost]
    [HasPermission(Permissions.AssignTrips)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateTripRequest request, CancellationToken cancellationToken)
    {
        var created = await _trips.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { tripId = created.Id }, created);
    }

    [HttpPost("{tripId:long}/reassign-vehicle")]
    [HasPermission(Permissions.AssignTrips)]
    public async Task<IActionResult> ReassignVehicle(long tripId, ReassignVehicleRequest request, CancellationToken cancellationToken)
    {
        await _trips.ReassignVehicleAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/reassign-driver")]
    [HasPermission(Permissions.AssignTrips)]
    public async Task<IActionResult> ReassignDriver(long tripId, ReassignDriverRequest request, CancellationToken cancellationToken)
    {
        await _trips.ReassignDriverAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{tripId:long}/history")]
    public Task<IReadOnlyList<StatusHistoryDto>> GetHistory(long tripId, CancellationToken cancellationToken) =>
        _trips.GetHistoryAsync(tripId, cancellationToken);

    [HttpGet("{tripId:long}/assignments")]
    [HasPermission(Permissions.ViewTrips)]
    public Task<IReadOnlyList<TripAssignmentDto>> GetAssignments(long tripId, CancellationToken cancellationToken) =>
        _trips.GetAssignmentsAsync(tripId, cancellationToken);

    /// <summary>Sends the pickup OTP by SMS to the pickup contact (not to the driver).</summary>
    [HttpPost("{tripId:long}/pickup/otp")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public Task<OtpSentResponse> SendPickupOtp(long tripId, CancellationToken cancellationToken) =>
        _trips.SendOtpAsync(tripId, VerificationType.Pickup, cancellationToken);

    [HttpPost("{tripId:long}/pickup/verify")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public async Task<IActionResult> VerifyPickup(long tripId, VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        await _trips.VerifyOtpAsync(tripId, VerificationType.Pickup, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/start")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    public async Task<IActionResult> Start(long tripId, StartTripRequest request, CancellationToken cancellationToken)
    {
        await _trips.StartAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends the delivery OTP by SMS to the delivery contact.</summary>
    [HttpPost("{tripId:long}/delivery/otp")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public Task<OtpSentResponse> SendDeliveryOtp(long tripId, CancellationToken cancellationToken) =>
        _trips.SendOtpAsync(tripId, VerificationType.Delivery, cancellationToken);

    [HttpPost("{tripId:long}/delivery/verify")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    public async Task<IActionResult> VerifyDelivery(long tripId, VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        await _trips.VerifyOtpAsync(tripId, VerificationType.Delivery, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/hold")]
    [HasPermission(Permissions.UpdateTrips)]
    public async Task<IActionResult> Hold(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        await _trips.HoldAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/resume")]
    [HasPermission(Permissions.UpdateTrips)]
    public async Task<IActionResult> Resume(long tripId, CancellationToken cancellationToken)
    {
        await _trips.ResumeAsync(tripId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/exception")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    public async Task<IActionResult> ReportException(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        await _trips.ReportExceptionAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/exception/resolve")]
    [HasPermission(Permissions.UpdateTrips)]
    public async Task<IActionResult> ResolveException(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        await _trips.ResolveExceptionAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/cancel")]
    [HasPermission(Permissions.UpdateTrips)]
    public async Task<IActionResult> Cancel(long tripId, TripReasonRequest request, CancellationToken cancellationToken)
    {
        await _trips.CancelAsync(tripId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Driver app posts location points (batched, at most 100 per call).</summary>
    [HttpPost("{tripId:long}/locations")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    public async Task<ActionResult<object>> AddLocations(long tripId, AddLocationsRequest request, CancellationToken cancellationToken) =>
        Ok(new { accepted = await _trips.AddLocationsAsync(tripId, request, cancellationToken) });

    /// <summary>Tracking for everyone involved in the trip. Pass <c>sinceUtc</c> to poll only new points.</summary>
    [HttpGet("{tripId:long}/tracking")]
    public Task<TripTrackingResponse> GetTracking(long tripId, [FromQuery] DateTime? sinceUtc, CancellationToken cancellationToken) =>
        _trips.GetTrackingAsync(tripId, sinceUtc, cancellationToken);

    /// <summary>Proof of delivery: receiver details + up to 5 photos and an optional signature image.</summary>
    [HttpPost("{tripId:long}/proof-of-delivery")]
    [HasAnyPermission(Permissions.PerformTrips, Permissions.UpdateTrips)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<CreatedResponse> UploadProofOfDelivery(long tripId, [FromForm] UploadProofOfDeliveryRequest request,
        [FromForm] List<IFormFile>? photos, IFormFile? signature, CancellationToken cancellationToken)
    {
        var uploads = (photos ?? []).Select(ToUpload).ToList();
        var id = await _trips.UploadProofOfDeliveryAsync(tripId, request, uploads, signature is null ? null : ToUpload(signature), cancellationToken);
        return new CreatedResponse(id, null);
    }

    [HttpGet("{tripId:long}/proof-of-delivery")]
    public Task<ProofOfDeliveryResponse> GetProofOfDelivery(long tripId, CancellationToken cancellationToken) =>
        _trips.GetProofOfDeliveryAsync(tripId, cancellationToken);

    [HttpGet("{tripId:long}/proof-of-delivery/files/{storedFileId:long}")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadProofOfDeliveryFile(long tripId, long storedFileId, CancellationToken cancellationToken) =>
        ToFileResult(await _trips.DownloadProofOfDeliveryFileAsync(tripId, storedFileId, cancellationToken));
}
