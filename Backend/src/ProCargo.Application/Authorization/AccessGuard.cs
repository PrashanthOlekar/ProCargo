using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.Constants;

namespace ProCargo.Application.Authorization;

/// <summary>
/// Resource-level authorization (protection against insecure direct object references).
///
/// Endpoint policies decide WHO may call an endpoint; this guard decides WHICH records they may touch:
///   * staff need the stated permission (and an Operations-portal token),
///   * customers only their own records (CustomerId claim),
///   * owners only their own fleet (OwnerId claim),
///   * drivers only themselves and trips assigned to them (DriverId claim).
///
/// When an external user asks for a record that is not theirs we answer 404 rather than 403, so ids of
/// other customers' records cannot be discovered by probing.
/// </summary>
public sealed class AccessGuard
{
    private readonly ICurrentUser _user;

    public AccessGuard(ICurrentUser user)
    {
        _user = user;
    }

    public ICurrentUser User => _user;

    public bool IsStaffWith(string permission) => _user.IsStaff && _user.HasPermission(permission);

    public void EnsureStaffPermission(string permission)
    {
        if (!IsStaffWith(permission))
        {
            throw new ForbiddenException($"The '{permission}' permission is required.");
        }
    }

    public long RequireCustomerId() =>
        _user.CustomerId ?? throw new ForbiddenException("A customer profile is required for this action.");

    public long RequireOwnerId() =>
        _user.OwnerId ?? throw new ForbiddenException("A vehicle owner profile is required for this action.");

    public long RequireDriverId() =>
        _user.DriverId ?? throw new ForbiddenException("A driver profile is required for this action.");

    public void EnsureCustomer(long customerId, string staffPermission, string entity = "Customer", object? id = null)
    {
        if (IsStaffWith(staffPermission)) return;
        if (_user.CustomerId == customerId) return;
        throw NotFoundException.For(entity, id ?? customerId);
    }

    public void EnsureOwner(long ownerId, string staffPermission, string entity = "Owner", object? id = null)
    {
        if (IsStaffWith(staffPermission)) return;
        if (_user.OwnerId == ownerId) return;
        throw NotFoundException.For(entity, id ?? ownerId);
    }

    /// <summary>A driver record is visible to the driver, to the owner who employs them, and to staff.</summary>
    public void EnsureDriver(long driverId, long? driverOwnerId, string staffPermission)
    {
        if (IsStaffWith(staffPermission)) return;
        if (_user.DriverId == driverId) return;
        if (_user.OwnerId is not null && driverOwnerId == _user.OwnerId) return;
        throw NotFoundException.For("Driver", driverId);
    }

    /// <summary>Booking: owning customer, the owner/driver assigned to its trip, or staff.</summary>
    public void EnsureBooking(long bookingId, long customerId, long? assignedOwnerId, long? assignedDriverId)
    {
        if (IsStaffWith(Permissions.ViewBookings)) return;
        if (_user.CustomerId == customerId) return;
        if (_user.OwnerId is not null && assignedOwnerId == _user.OwnerId) return;
        if (_user.DriverId is not null && assignedDriverId == _user.DriverId) return;
        throw NotFoundException.For("Booking", bookingId);
    }

    /// <summary>Trip: customer of the booking, owner of the vehicle, assigned driver, or staff.</summary>
    public void EnsureTrip(long tripId, long customerId, long ownerId, long driverId)
    {
        if (IsStaffWith(Permissions.ViewTrips)) return;
        if (_user.CustomerId == customerId) return;
        if (_user.OwnerId == ownerId) return;
        if (_user.DriverId == driverId) return;
        throw NotFoundException.For("Trip", tripId);
    }

    /// <summary>Only the assigned driver (or operations staff) may operate a trip.</summary>
    public void EnsureCanOperateTrip(long tripId, long driverId)
    {
        if (IsStaffWith(Permissions.UpdateTrips)) return;
        if (_user.DriverId == driverId && _user.HasPermission(Permissions.PerformTrips)) return;
        throw NotFoundException.For("Trip", tripId);
    }

    /// <summary>
    /// For list endpoints: external users are always scoped to their own profile regardless of the filters
    /// they send. Returns (customerId, ownerId, driverId) filters to apply.
    /// </summary>
    public (long? CustomerId, long? OwnerId, long? DriverId) ScopeFilters(string staffPermission,
        long? requestedCustomerId = null, long? requestedOwnerId = null, long? requestedDriverId = null)
    {
        if (IsStaffWith(staffPermission))
        {
            return (requestedCustomerId, requestedOwnerId, requestedDriverId);
        }

        if (_user.CustomerId is not null) return (_user.CustomerId, null, null);
        if (_user.OwnerId is not null) return (null, _user.OwnerId, null);
        if (_user.DriverId is not null) return (null, null, _user.DriverId);

        throw new ForbiddenException();
    }
}
