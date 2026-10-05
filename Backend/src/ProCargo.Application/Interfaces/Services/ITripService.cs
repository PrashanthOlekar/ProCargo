using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Services;

public interface ITripService
{
    Task<PagedResult<TripListItemDto>> GetPagedAsync(TripSearchRequest request, CancellationToken cancellationToken);
    Task<TripDetailsResponse> GetByIdAsync(long tripId, CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken);
    Task ReassignVehicleAsync(long tripId, ReassignVehicleRequest request, CancellationToken cancellationToken);
    Task ReassignDriverAsync(long tripId, ReassignDriverRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripAssignmentDto>> GetAssignmentsAsync(long tripId, CancellationToken cancellationToken);

    Task<OtpSentResponse> SendOtpAsync(long tripId, VerificationType type, CancellationToken cancellationToken);
    Task VerifyOtpAsync(long tripId, VerificationType type, VerifyOtpRequest request, CancellationToken cancellationToken);

    Task StartAsync(long tripId, StartTripRequest request, CancellationToken cancellationToken);
    Task HoldAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken);
    Task ResumeAsync(long tripId, CancellationToken cancellationToken);
    Task ReportExceptionAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken);
    Task ResolveExceptionAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken);
    Task CancelAsync(long tripId, TripReasonRequest request, CancellationToken cancellationToken);

    Task<int> AddLocationsAsync(long tripId, AddLocationsRequest request, CancellationToken cancellationToken);
    Task<TripTrackingResponse> GetTrackingAsync(long tripId, DateTime? sinceUtc, CancellationToken cancellationToken);

    Task<long> UploadProofOfDeliveryAsync(long tripId, UploadProofOfDeliveryRequest request, IReadOnlyList<FileUpload> photos,
        FileUpload? signature, CancellationToken cancellationToken);
    Task<ProofOfDeliveryResponse> GetProofOfDeliveryAsync(long tripId, CancellationToken cancellationToken);
    Task<FileDownload> DownloadProofOfDeliveryFileAsync(long tripId, long storedFileId, CancellationToken cancellationToken);
}
