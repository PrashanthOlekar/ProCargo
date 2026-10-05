using System.Text.Json;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

/// <summary>
/// Booking persistence. Items are sent to the procedures as a JSON array (read with OPENJSON), so a booking
/// and all of its items are written in one call and one SQL transaction.
/// </summary>
internal sealed class BookingRepository : IBookingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly StoredProcedureExecutor _sp;

    public BookingRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<BookingListItemDto>> GetPagedAsync(BookingSearchRequest request, BookingScope scope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "BookingNumber", "PickupDate");
        var rows = await _sp.QueryAsync<BookingListItemDto>($"""
            EXEC core.usp_Booking_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search},
                @CustomerId={scope.CustomerId}, @OwnerId={scope.OwnerId}, @DriverId={scope.DriverId},
                @BookingStatusId={(int?)request.Status}, @VehicleTypeId={request.VehicleTypeId},
                @FromDateUtc={request.FromDateUtc}, @ToDateUtc={request.ToDateUtc}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<BookingDetailsDto?> GetByIdAsync(long bookingId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<BookingDetailsDto>($"EXEC core.usp_Booking_GetById @BookingId={bookingId}", cancellationToken);

    public Task<IReadOnlyList<BookingItemDto>> GetItemsAsync(long bookingId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<BookingItemDto>($"EXEC core.usp_BookingItem_GetByBooking @BookingId={bookingId}", cancellationToken);

    public Task<CreatedResult> CreateAsync(SaveBookingCommand c, CancellationToken cancellationToken)
    {
        var r = c.Request;
        return _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC core.usp_Booking_Create
                @CustomerId={c.CustomerId}, @VehicleTypeId={r.VehicleTypeId}, @GoodsTypeId={r.GoodsTypeId},
                @GoodsDescription={r.GoodsDescription.Trim()}, @RequestedPickupDateUtc={r.RequestedPickupDateUtc},
                @SpecialInstructions={r.SpecialInstructions?.Trim()}, @EstimatedDistanceKm={c.EstimatedDistanceKm}, @Submit={c.Submit},
                @PickupAddressLine1={r.PickupAddress.AddressLine1.Trim()}, @PickupAddressLine2={r.PickupAddress.AddressLine2?.Trim()},
                @PickupLandmark={r.PickupAddress.Landmark?.Trim()}, @PickupCityId={r.PickupAddress.CityId}, @PickupPincode={r.PickupAddress.Pincode},
                @PickupLatitude={SqlParams.Coordinate("PickupLatitude", r.PickupAddress.Latitude)},
                @PickupLongitude={SqlParams.Coordinate("PickupLongitude", r.PickupAddress.Longitude)},
                @DeliveryAddressLine1={r.DeliveryAddress.AddressLine1.Trim()}, @DeliveryAddressLine2={r.DeliveryAddress.AddressLine2?.Trim()},
                @DeliveryLandmark={r.DeliveryAddress.Landmark?.Trim()}, @DeliveryCityId={r.DeliveryAddress.CityId}, @DeliveryPincode={r.DeliveryAddress.Pincode},
                @DeliveryLatitude={SqlParams.Coordinate("DeliveryLatitude", r.DeliveryAddress.Latitude)},
                @DeliveryLongitude={SqlParams.Coordinate("DeliveryLongitude", r.DeliveryAddress.Longitude)},
                @PickupContactName={r.PickupContact.ContactName.Trim()}, @PickupContactPhone={c.PickupContactPhone},
                @PickupContactAltPhone={c.PickupContactAltPhone},
                @DeliveryContactName={r.DeliveryContact.ContactName.Trim()}, @DeliveryContactPhone={c.DeliveryContactPhone},
                @DeliveryContactAltPhone={c.DeliveryContactAltPhone},
                @ItemsJson={SqlParams.Json("ItemsJson", ItemsJson(r.Items))}, @CreatedBy={c.UserId}
            """, cancellationToken);
    }

    public Task UpdateAsync(SaveBookingCommand c, CancellationToken cancellationToken)
    {
        var r = c.Request;
        return _sp.ExecuteAsync($"""
            EXEC core.usp_Booking_Update
                @BookingId={c.BookingId}, @VehicleTypeId={r.VehicleTypeId}, @GoodsTypeId={r.GoodsTypeId},
                @GoodsDescription={r.GoodsDescription.Trim()}, @RequestedPickupDateUtc={r.RequestedPickupDateUtc},
                @SpecialInstructions={r.SpecialInstructions?.Trim()}, @EstimatedDistanceKm={c.EstimatedDistanceKm},
                @PickupAddressLine1={r.PickupAddress.AddressLine1.Trim()}, @PickupAddressLine2={r.PickupAddress.AddressLine2?.Trim()},
                @PickupLandmark={r.PickupAddress.Landmark?.Trim()}, @PickupCityId={r.PickupAddress.CityId}, @PickupPincode={r.PickupAddress.Pincode},
                @PickupLatitude={SqlParams.Coordinate("PickupLatitude", r.PickupAddress.Latitude)},
                @PickupLongitude={SqlParams.Coordinate("PickupLongitude", r.PickupAddress.Longitude)},
                @DeliveryAddressLine1={r.DeliveryAddress.AddressLine1.Trim()}, @DeliveryAddressLine2={r.DeliveryAddress.AddressLine2?.Trim()},
                @DeliveryLandmark={r.DeliveryAddress.Landmark?.Trim()}, @DeliveryCityId={r.DeliveryAddress.CityId}, @DeliveryPincode={r.DeliveryAddress.Pincode},
                @DeliveryLatitude={SqlParams.Coordinate("DeliveryLatitude", r.DeliveryAddress.Latitude)},
                @DeliveryLongitude={SqlParams.Coordinate("DeliveryLongitude", r.DeliveryAddress.Longitude)},
                @PickupContactName={r.PickupContact.ContactName.Trim()}, @PickupContactPhone={c.PickupContactPhone},
                @PickupContactAltPhone={c.PickupContactAltPhone},
                @DeliveryContactName={r.DeliveryContact.ContactName.Trim()}, @DeliveryContactPhone={c.DeliveryContactPhone},
                @DeliveryContactAltPhone={c.DeliveryContactAltPhone},
                @ItemsJson={SqlParams.Json("ItemsJson", ItemsJson(r.Items))}, @ModifiedBy={c.UserId}, @RowVersion={c.RowVersion}
            """, cancellationToken);
    }

    public Task ChangeStatusAsync(long bookingId, BookingStatus expected, BookingStatus next, string? remarks, long changedBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Booking_ChangeStatus @BookingId={bookingId}, @ExpectedStatusId={(int)expected}, @NewStatusId={(int)next},
                @Remarks={remarks}, @ChangedBy={changedBy}
            """, cancellationToken);

    public Task CancelAsync(long bookingId, BookingStatus expected, string reason, long changedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Booking_Cancel @BookingId={bookingId}, @ExpectedStatusId={(int)expected}, @Reason={reason}, @ChangedBy={changedBy}
            """, cancellationToken);

    public Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long bookingId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<StatusHistoryDto>($"EXEC core.usp_Booking_GetStatusHistory @BookingId={bookingId}", cancellationToken);

    public async Task<long> AddNoteAsync(long bookingId, string note, bool isInternal, long createdBy, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_BookingNote_Create @BookingId={bookingId}, @Note={note}, @IsInternal={isInternal}, @CreatedBy={createdBy}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<BookingNoteDto>> GetNotesAsync(long bookingId, bool includeInternal, CancellationToken cancellationToken) =>
        _sp.QueryAsync<BookingNoteDto>($"EXEC core.usp_BookingNote_GetByBooking @BookingId={bookingId}, @IncludeInternal={includeInternal}", cancellationToken);

    private static string ItemsJson(IEnumerable<BookingItemRequest> items) =>
        JsonSerializer.Serialize(items.Select(i => new
        {
            description = i.Description.Trim(),
            quantity = i.Quantity,
            weightKg = i.WeightKg,
            lengthCm = i.LengthCm,
            widthCm = i.WidthCm,
            heightCm = i.HeightCm,
            isFragile = i.IsFragile,
            declaredValue = i.DeclaredValue
        }), JsonOptions);
}
