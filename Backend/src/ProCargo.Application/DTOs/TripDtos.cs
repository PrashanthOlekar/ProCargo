using System.Text.Json.Serialization;
using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

public sealed class TripListItemDto : PagedRow
{
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string VehicleNumber { get; init; } = string.Empty;
    public string DriverName { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string PickupCityName { get; init; } = string.Empty;
    public string DeliveryCityName { get; init; } = string.Empty;
    public int TripStatusId { get; init; }
    public DateTime PlannedPickupDateUtc { get; init; }
    public DateTime PlannedDeliveryDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class TripDto
{
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public int BookingStatusId { get; init; }
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    [JsonIgnore] public long CustomerUserId { get; init; }
    public long QuotationId { get; init; }
    public long VehicleId { get; init; }
    public string VehicleNumber { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public string DriverPhoneNumber { get; init; } = string.Empty;
    [JsonIgnore] public long? DriverUserId { get; init; }
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    [JsonIgnore] public long OwnerUserId { get; init; }
    public int TripStatusId { get; init; }
    public int? StatusBeforeHoldId { get; init; }
    public DateTime PlannedPickupDateUtc { get; init; }
    public DateTime? ActualPickupDateUtc { get; init; }
    public DateTime PlannedDeliveryDateUtc { get; init; }
    public DateTime? ActualDeliveryDateUtc { get; init; }
    public int? StartOdometer { get; init; }
    public int? EndOdometer { get; init; }
    public string? ExceptionReason { get; init; }
    public string? CancellationReason { get; init; }
    public string PickupAddress { get; init; } = string.Empty;
    public string PickupCityName { get; init; } = string.Empty;
    public decimal? PickupLatitude { get; init; }
    public decimal? PickupLongitude { get; init; }
    public string DeliveryAddress { get; init; } = string.Empty;
    public string DeliveryCityName { get; init; } = string.Empty;
    public decimal? DeliveryLatitude { get; init; }
    public decimal? DeliveryLongitude { get; init; }
    public string PickupContactName { get; init; } = string.Empty;
    public string PickupContactPhone { get; init; } = string.Empty;
    public string DeliveryContactName { get; init; } = string.Empty;
    public string DeliveryContactPhone { get; init; } = string.Empty;
    public string GoodsDescription { get; init; } = string.Empty;
    public decimal TotalWeightKg { get; init; }
    public int TotalQuantity { get; init; }
    public string? SpecialInstructions { get; init; }
    public bool HasProofOfDelivery { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record TripDetailsResponse(TripDto Trip, IReadOnlyList<string> AvailableActions);

public sealed class TripAssignmentDto
{
    public long TripAssignmentId { get; init; }
    public int AssignmentTypeId { get; init; }
    public long? VehicleId { get; init; }
    public string? VehicleNumber { get; init; }
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long AssignedBy { get; init; }
    public string? AssignedByName { get; init; }
    public DateTime AssignedDateUtc { get; init; }
    public DateTime? ReleasedDateUtc { get; init; }
    public string? Reason { get; init; }
}

public sealed class TripVerificationRow
{
    public long TripVerificationId { get; init; }
    public byte[] OtpHash { get; init; } = [];
    public DateTime ExpiresDateUtc { get; init; }
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public bool IsVerified { get; init; }
    public string SentToPhoneMasked { get; init; } = string.Empty;
}

public sealed record OtpSentResponse(string SentTo, DateTime ExpiresDateUtc)
{
    /// <summary>Only populated when Security:ExposeOtpForTesting is enabled (never in Production).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TestOtp { get; init; }
}

public sealed class TripLocationDto
{
    public long TripLocationHistoryId { get; init; }
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public DateTime RecordedDateUtc { get; init; }
    public decimal? SpeedKmph { get; init; }
    public decimal? Heading { get; init; }
    public decimal? Accuracy { get; init; }
    public int TrackingProviderId { get; init; }
}

public sealed record TripTrackingResponse(long TripId, int TripStatusId, TripLocationDto? LastLocation, IReadOnlyList<TripLocationDto> Points);

public sealed class ProofOfDeliveryDto
{
    public long ProofOfDeliveryId { get; init; }
    public long TripId { get; init; }
    public string ReceiverName { get; init; } = string.Empty;
    public string? ReceiverPhone { get; init; }
    public DateTime DeliveredDateUtc { get; init; }
    public string? Remarks { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public long? SignatureFileId { get; init; }
    public long UploadedBy { get; init; }
    public string? UploadedByName { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class ProofOfDeliveryFileDto
{
    public long ProofOfDeliveryFileId { get; init; }
    public long StoredFileId { get; init; }
    public string FileCategory { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed record ProofOfDeliveryResponse(ProofOfDeliveryDto ProofOfDelivery, IReadOnlyList<ProofOfDeliveryFileDto> Files);
