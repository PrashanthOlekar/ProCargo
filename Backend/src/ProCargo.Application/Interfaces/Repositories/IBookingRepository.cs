using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

/// <summary>Everything the create/update procedures need, already normalised by the service.</summary>
public sealed record SaveBookingCommand(
    long? BookingId,
    long CustomerId,
    BookingRequestBase Request,
    decimal? EstimatedDistanceKm,
    string PickupContactPhone,
    string? PickupContactAltPhone,
    string DeliveryContactPhone,
    string? DeliveryContactAltPhone,
    bool Submit,
    byte[]? RowVersion,
    long UserId);

public sealed record BookingScope(long? CustomerId, long? OwnerId, long? DriverId);

public interface IBookingRepository
{
    Task<PagedResult<BookingListItemDto>> GetPagedAsync(BookingSearchRequest request, BookingScope scope, CancellationToken cancellationToken);
    Task<BookingDetailsDto?> GetByIdAsync(long bookingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingItemDto>> GetItemsAsync(long bookingId, CancellationToken cancellationToken);
    Task<CreatedResult> CreateAsync(SaveBookingCommand command, CancellationToken cancellationToken);
    Task UpdateAsync(SaveBookingCommand command, CancellationToken cancellationToken);
    Task ChangeStatusAsync(long bookingId, BookingStatus expected, BookingStatus next, string? remarks, long changedBy, CancellationToken cancellationToken);
    Task CancelAsync(long bookingId, BookingStatus expected, string reason, long changedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long bookingId, CancellationToken cancellationToken);
    Task<long> AddNoteAsync(long bookingId, string note, bool isInternal, long createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingNoteDto>> GetNotesAsync(long bookingId, bool includeInternal, CancellationToken cancellationToken);
}
