using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

public sealed class BookingSearchRequest : PagedRequest
{
    public BookingStatus? Status { get; set; }
    public int? VehicleTypeId { get; set; }

    /// <summary>Staff only; external users are always scoped to their own records.</summary>
    public long? CustomerId { get; set; }

    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }
}

public sealed class BookingAddressRequest
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public int CityId { get; set; }
    public string Pincode { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class BookingContactRequest
{
    public string ContactName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhoneNumber { get; set; }
}

public sealed class BookingItemRequest
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal WeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public bool IsFragile { get; set; }
    public decimal? DeclaredValue { get; set; }
}

/// <summary>Shared shape of create / update booking requests.</summary>
public abstract class BookingRequestBase
{
    public int VehicleTypeId { get; set; }
    public int GoodsTypeId { get; set; }
    public string GoodsDescription { get; set; } = string.Empty;
    public DateTime RequestedPickupDateUtc { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal? EstimatedDistanceKm { get; set; }
    public BookingAddressRequest PickupAddress { get; set; } = new();
    public BookingAddressRequest DeliveryAddress { get; set; } = new();
    public BookingContactRequest PickupContact { get; set; } = new();
    public BookingContactRequest DeliveryContact { get; set; } = new();
    public List<BookingItemRequest> Items { get; set; } = [];
}

public sealed class CreateBookingRequest : BookingRequestBase
{
    /// <summary>Staff creating a booking on behalf of a customer. Ignored for customers.</summary>
    public long? CustomerId { get; set; }

    /// <summary>true = submit for review now; false = save as draft.</summary>
    public bool Submit { get; set; } = true;
}

public sealed class UpdateBookingRequest : BookingRequestBase
{
    public byte[] RowVersion { get; set; } = [];
}

public sealed class BookingActionRequest
{
    public string? Remarks { get; set; }
}

public sealed class CancelBookingRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class AddBookingNoteRequest
{
    public string Note { get; set; } = string.Empty;

    /// <summary>Internal notes are visible to staff only. External users always create public notes.</summary>
    public bool IsInternal { get; set; } = true;
}
