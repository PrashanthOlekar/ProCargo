using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

public sealed record TripScope(long? CustomerId, long? OwnerId, long? DriverId);

public sealed record TripStatusChange(
    long TripId,
    TripStatus Expected,
    TripStatus New,
    string? Remarks,
    long ChangedBy,
    int? Odometer = null,
    BookingStatus? BookingExpected = null,
    BookingStatus? BookingNew = null);

public sealed record CreateVerificationCommand(
    long TripId,
    VerificationType Type,
    byte[] OtpHash,
    string SentToPhoneMasked,
    DateTime ExpiresDateUtc,
    int MaxAttempts,
    int MinResendSeconds,
    long CreatedBy);

public sealed record VerificationAttempt(
    long TripVerificationId,
    bool IsSuccess,
    long VerifiedBy,
    string? ReceiverName,
    decimal? Latitude,
    decimal? Longitude);

public sealed record CreateProofOfDeliveryCommand(
    long TripId,
    string ReceiverName,
    string? ReceiverPhone,
    string? Remarks,
    decimal? Latitude,
    decimal? Longitude,
    long? SignatureFileId,
    IReadOnlyList<(long StoredFileId, string Category)> Files,
    long UploadedBy);

public interface ITripRepository
{
    Task<PagedResult<TripListItemDto>> GetPagedAsync(TripSearchRequest request, TripScope scope, CancellationToken cancellationToken);
    Task<TripDto?> GetByIdAsync(long tripId, CancellationToken cancellationToken);
    Task<CreatedResult> CreateAsync(CreateTripRequest request, long createdBy, CancellationToken cancellationToken);
    Task AssignVehicleAsync(long tripId, long vehicleId, string reason, long assignedBy, CancellationToken cancellationToken);
    Task AssignDriverAsync(long tripId, long driverId, string reason, long assignedBy, CancellationToken cancellationToken);
    Task UpdateStatusAsync(TripStatusChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripAssignmentDto>> GetAssignmentsAsync(long tripId, CancellationToken cancellationToken);

    Task<long> CreateVerificationAsync(CreateVerificationCommand command, CancellationToken cancellationToken);
    Task<TripVerificationRow?> GetActiveVerificationAsync(long tripId, VerificationType type, CancellationToken cancellationToken);
    Task<int> RegisterVerificationAttemptAsync(VerificationAttempt attempt, CancellationToken cancellationToken);

    Task<int> AddLocationsAsync(long tripId, TrackingProviderType provider, IReadOnlyList<LocationPointRequest> points, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripLocationDto>> GetLocationsAsync(long tripId, DateTime? sinceUtc, int maxPoints, CancellationToken cancellationToken);

    Task<long> CreateProofOfDeliveryAsync(CreateProofOfDeliveryCommand command, CancellationToken cancellationToken);
    Task<ProofOfDeliveryDto?> GetProofOfDeliveryAsync(long tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProofOfDeliveryFileDto>> GetProofOfDeliveryFilesAsync(long tripId, CancellationToken cancellationToken);
}
