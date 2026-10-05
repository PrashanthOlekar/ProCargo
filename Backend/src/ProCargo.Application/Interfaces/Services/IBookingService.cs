using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Interfaces.Services;

public interface IBookingService
{
    Task<PagedResult<BookingListItemDto>> GetPagedAsync(BookingSearchRequest request, CancellationToken cancellationToken);
    Task<BookingDetailsResponse> GetByIdAsync(long bookingId, CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long bookingId, UpdateBookingRequest request, CancellationToken cancellationToken);
    Task SubmitAsync(long bookingId, CancellationToken cancellationToken);
    Task ReviewAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken);
    Task HoldAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken);
    Task ResumeAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken);
    Task RejectAsync(long bookingId, BookingActionRequest request, CancellationToken cancellationToken);
    Task CancelAsync(long bookingId, CancelBookingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long bookingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingNoteDto>> GetNotesAsync(long bookingId, CancellationToken cancellationToken);
    Task<long> AddNoteAsync(long bookingId, AddBookingNoteRequest request, CancellationToken cancellationToken);

    /// <summary>Loads a booking after checking that the caller may see it. Used by other services.</summary>
    Task<BookingDetailsDto> GetAccessibleAsync(long bookingId, CancellationToken cancellationToken);
}
