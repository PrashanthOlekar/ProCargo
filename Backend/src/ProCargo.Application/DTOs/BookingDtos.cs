using System.Text.Json.Serialization;
using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

/// <summary>core.usp_Booking_GetPaged</summary>
public sealed class BookingListItemDto : PagedRow
{
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public decimal TotalWeightKg { get; init; }
    public string PickupCityName { get; init; } = string.Empty;
    public string DeliveryCityName { get; init; } = string.Empty;
    public DateTime RequestedPickupDateUtc { get; init; }
    public int BookingStatusId { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

/// <summary>core.usp_Booking_GetById - header, addresses, contacts and current trip/invoice summary in one row.</summary>
public sealed class BookingDetailsDto
{
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;

    [JsonIgnore]
    public long CustomerUserId { get; init; }

    public string? CustomerCompanyName { get; init; }
    public string CustomerPhoneNumber { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public int GoodsTypeId { get; init; }
    public string GoodsTypeName { get; init; } = string.Empty;
    public bool RequiresSpecialHandling { get; init; }
    public string GoodsDescription { get; init; } = string.Empty;
    public decimal TotalWeightKg { get; init; }
    public int TotalQuantity { get; init; }
    public DateTime RequestedPickupDateUtc { get; init; }
    public string? SpecialInstructions { get; init; }
    public decimal? EstimatedDistanceKm { get; init; }
    public int BookingStatusId { get; init; }
    public int? StatusBeforeHoldId { get; init; }
    public string? CancellationReason { get; init; }

    public string PickupAddressLine1 { get; init; } = string.Empty;
    public string? PickupAddressLine2 { get; init; }
    public string? PickupLandmark { get; init; }
    public int PickupCityId { get; init; }
    public string PickupCityName { get; init; } = string.Empty;
    public int PickupStateId { get; init; }
    public string PickupPincode { get; init; } = string.Empty;
    public decimal? PickupLatitude { get; init; }
    public decimal? PickupLongitude { get; init; }

    public string DeliveryAddressLine1 { get; init; } = string.Empty;
    public string? DeliveryAddressLine2 { get; init; }
    public string? DeliveryLandmark { get; init; }
    public int DeliveryCityId { get; init; }
    public string DeliveryCityName { get; init; } = string.Empty;
    public int DeliveryStateId { get; init; }
    public string DeliveryPincode { get; init; } = string.Empty;
    public decimal? DeliveryLatitude { get; init; }
    public decimal? DeliveryLongitude { get; init; }

    public string PickupContactName { get; init; } = string.Empty;
    public string PickupContactPhone { get; init; } = string.Empty;
    public string? PickupContactAltPhone { get; init; }
    public string DeliveryContactName { get; init; } = string.Empty;
    public string DeliveryContactPhone { get; init; } = string.Empty;
    public string? DeliveryContactAltPhone { get; init; }

    public long? AcceptedQuotationId { get; init; }
    public decimal? AcceptedQuotationAmount { get; init; }
    public long? TripId { get; init; }
    public string? TripNumber { get; init; }
    public int? TripStatusId { get; init; }
    public long? AssignedOwnerId { get; init; }
    public long? AssignedDriverId { get; init; }
    public string? AssignedVehicleNumber { get; init; }
    public string? AssignedDriverName { get; init; }
    public string? AssignedDriverPhone { get; init; }
    public long? InvoiceId { get; init; }

    public DateTime CreatedDateUtc { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

/// <summary>core.usp_BookingItem_GetByBooking</summary>
public sealed class BookingItemDto
{
    public long BookingItemId { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal WeightKg { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public bool IsFragile { get; init; }
    public decimal? DeclaredValue { get; init; }
}

/// <summary>core.usp_BookingNote_GetByBooking</summary>
public sealed class BookingNoteDto
{
    public long BookingNoteId { get; init; }
    public string Note { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public long CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

/// <summary>
/// Booking with its items and the actions the CURRENT user may perform right now. Front-ends use
/// <see cref="AvailableActions"/> only to show/hide buttons; every action is re-checked by the API.
/// </summary>
public sealed record BookingDetailsResponse(BookingDetailsDto Booking, IReadOnlyList<BookingItemDto> Items, IReadOnlyList<string> AvailableActions);
