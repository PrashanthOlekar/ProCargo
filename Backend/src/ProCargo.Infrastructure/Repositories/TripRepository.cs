using System.Text.Json;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class TripRepository : ITripRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly StoredProcedureExecutor _sp;

    public TripRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<TripListItemDto>> GetPagedAsync(TripSearchRequest request, TripScope scope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "TripNumber", "PickupDate");
        var rows = await _sp.QueryAsync<TripListItemDto>($"""
            EXEC core.usp_Trip_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search},
                @CustomerId={scope.CustomerId}, @OwnerId={scope.OwnerId}, @DriverId={scope.DriverId},
                @TripStatusId={(int?)request.Status}, @ActiveOnly={request.ActiveOnly},
                @FromDateUtc={request.FromDateUtc}, @ToDateUtc={request.ToDateUtc}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<TripDto?> GetByIdAsync(long tripId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<TripDto>($"EXEC core.usp_Trip_GetById @TripId={tripId}", cancellationToken);

    public Task<CreatedResult> CreateAsync(CreateTripRequest r, long createdBy, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC core.usp_Trip_Create @BookingId={r.BookingId}, @VehicleId={r.VehicleId}, @DriverId={r.DriverId},
                @PlannedPickupDateUtc={r.PlannedPickupDateUtc}, @PlannedDeliveryDateUtc={r.PlannedDeliveryDateUtc}, @CreatedBy={createdBy}
            """, cancellationToken);

    public Task AssignVehicleAsync(long tripId, long vehicleId, string reason, long assignedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Trip_AssignVehicle @TripId={tripId}, @VehicleId={vehicleId}, @Reason={reason}, @AssignedBy={assignedBy}
            """, cancellationToken);

    public Task AssignDriverAsync(long tripId, long driverId, string reason, long assignedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Trip_AssignDriver @TripId={tripId}, @DriverId={driverId}, @Reason={reason}, @AssignedBy={assignedBy}
            """, cancellationToken);

    public Task UpdateStatusAsync(TripStatusChange c, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Trip_UpdateStatus @TripId={c.TripId}, @ExpectedStatusId={(int)c.Expected}, @NewStatusId={(int)c.New},
                @Remarks={c.Remarks}, @ChangedBy={c.ChangedBy}, @Odometer={c.Odometer},
                @BookingExpectedStatusId={(int?)c.BookingExpected}, @BookingNewStatusId={(int?)c.BookingNew}
            """, cancellationToken);

    public Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long tripId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<StatusHistoryDto>($"EXEC core.usp_Trip_GetStatusHistory @TripId={tripId}", cancellationToken);

    public Task<IReadOnlyList<TripAssignmentDto>> GetAssignmentsAsync(long tripId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<TripAssignmentDto>($"EXEC core.usp_Trip_GetAssignments @TripId={tripId}", cancellationToken);

    public async Task<long> CreateVerificationAsync(CreateVerificationCommand c, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_TripVerification_Create @TripId={c.TripId}, @VerificationTypeId={(int)c.Type}, @OtpHash={c.OtpHash},
                @SentToPhoneMasked={c.SentToPhoneMasked}, @ExpiresDateUtc={c.ExpiresDateUtc}, @MaxAttempts={c.MaxAttempts},
                @MinResendSeconds={c.MinResendSeconds}, @CreatedBy={c.CreatedBy}
            """, cancellationToken);
        return result.Id;
    }

    public Task<TripVerificationRow?> GetActiveVerificationAsync(long tripId, VerificationType type, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<TripVerificationRow>(
            $"EXEC core.usp_TripVerification_GetActive @TripId={tripId}, @VerificationTypeId={(int)type}", cancellationToken);

    public async Task<int> RegisterVerificationAttemptAsync(VerificationAttempt a, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_TripVerification_RegisterAttempt @TripVerificationId={a.TripVerificationId}, @IsSuccess={a.IsSuccess},
                @VerifiedBy={a.VerifiedBy}, @ReceiverName={a.ReceiverName},
                @Latitude={SqlParams.Coordinate("Latitude", a.Latitude)}, @Longitude={SqlParams.Coordinate("Longitude", a.Longitude)}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> AddLocationsAsync(long tripId, TrackingProviderType provider, IReadOnlyList<LocationPointRequest> points,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(points.Select(p => new
        {
            latitude = p.Latitude,
            longitude = p.Longitude,
            recordedDateUtc = DateTime.SpecifyKind(p.RecordedDateUtc, DateTimeKind.Utc),
            speedKmph = p.SpeedKmph,
            heading = p.Heading,
            accuracy = p.Accuracy
        }), JsonOptions);

        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_TripLocation_AddBatch @TripId={tripId}, @TrackingProviderId={(int)provider},
                @PointsJson={SqlParams.Json("PointsJson", json)}
            """, cancellationToken);
        return (int)result.Id;
    }

    public Task<IReadOnlyList<TripLocationDto>> GetLocationsAsync(long tripId, DateTime? sinceUtc, int maxPoints, CancellationToken cancellationToken) =>
        _sp.QueryAsync<TripLocationDto>(
            $"EXEC core.usp_TripLocation_GetByTrip @TripId={tripId}, @SinceUtc={sinceUtc}, @MaxPoints={maxPoints}", cancellationToken);

    public async Task<long> CreateProofOfDeliveryAsync(CreateProofOfDeliveryCommand c, CancellationToken cancellationToken)
    {
        var files = JsonSerializer.Serialize(c.Files.Select(f => new { storedFileId = f.StoredFileId, fileCategory = f.Category }), JsonOptions);
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_ProofOfDelivery_Create @TripId={c.TripId}, @ReceiverName={c.ReceiverName}, @ReceiverPhone={c.ReceiverPhone},
                @Remarks={c.Remarks}, @Latitude={SqlParams.Coordinate("Latitude", c.Latitude)},
                @Longitude={SqlParams.Coordinate("Longitude", c.Longitude)}, @SignatureFileId={c.SignatureFileId},
                @FilesJson={SqlParams.Json("FilesJson", files)}, @UploadedBy={c.UploadedBy}
            """, cancellationToken);
        return result.Id;
    }

    public Task<ProofOfDeliveryDto?> GetProofOfDeliveryAsync(long tripId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<ProofOfDeliveryDto>($"EXEC core.usp_ProofOfDelivery_GetByTrip @TripId={tripId}", cancellationToken);

    public Task<IReadOnlyList<ProofOfDeliveryFileDto>> GetProofOfDeliveryFilesAsync(long tripId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<ProofOfDeliveryFileDto>($"EXEC core.usp_ProofOfDeliveryFile_GetByTrip @TripId={tripId}", cancellationToken);
}
