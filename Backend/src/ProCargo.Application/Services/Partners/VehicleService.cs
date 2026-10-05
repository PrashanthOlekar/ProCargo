using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// Fleet management. Owners manage their own vehicles; staff manage and verify all vehicles.
/// A vehicle can only be offered for trips after verification with valid insurance.
/// </summary>
public sealed class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicles;
    private readonly IOwnerRepository _owners;
    private readonly IMasterDataRepository _masterData;
    private readonly VerificationRules _verification;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public VehicleService(IVehicleRepository vehicles, IOwnerRepository owners, IMasterDataRepository masterData, VerificationRules verification,
        INotificationService notifications, AccessGuard access, IAuditLogger audit, TimeProvider clock)
    {
        _vehicles = vehicles;
        _owners = owners;
        _masterData = masterData;
        _verification = verification;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public Task<PagedResult<VehicleListItemDto>> GetPagedAsync(VehicleSearchRequest request, CancellationToken cancellationToken)
    {
        if (_access.IsStaffWith(Permissions.ViewVehicles))
        {
            return _vehicles.GetPagedAsync(request, null, cancellationToken);
        }

        return _vehicles.GetPagedAsync(request, _access.RequireOwnerId(), cancellationToken);
    }

    public async Task<VehicleDto> GetByIdAsync(long vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken) ?? throw NotFoundException.For("Vehicle", vehicleId);
        _access.EnsureOwner(vehicle.OwnerId, Permissions.ViewVehicles, "Vehicle", vehicleId);
        return vehicle;
    }

    public async Task<long> CreateAsync(CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        long ownerId;
        if (_access.IsStaffWith(Permissions.ManageVehicles))
        {
            ownerId = request.OwnerId ?? throw new RequestValidationException(nameof(request.OwnerId), "OwnerId is required.");
            _ = await _owners.GetByIdAsync(ownerId, cancellationToken) ?? throw NotFoundException.For("Owner", ownerId);
        }
        else
        {
            if (!_access.User.HasPermission(Permissions.ManageOwnFleet)) throw new ForbiddenException();
            ownerId = _access.RequireOwnerId();
        }

        await EnsureVehicleTypeAsync(request.VehicleTypeId, request.CapacityKg, cancellationToken);

        var id = await _vehicles.CreateAsync(new SaveVehicleCommand(
            null, ownerId, IndianFormats.NormalizeVehicleNumber(request.VehicleNumber), request.VehicleTypeId,
            request.Manufacturer.Trim(), request.Model.Trim(), request.ManufactureYear, request.CapacityKg,
            request.PermitNumber?.Trim(), request.PermitExpiryDate, request.InsuranceNumber?.Trim(), request.InsuranceExpiryDate,
            request.FitnessExpiryDate, request.PucExpiryDate, null, _access.User.UserId), cancellationToken);

        await _audit.LogAsync("VehicleCreated", "Vehicle", id,
            newValue: new { VehicleNumber = IndianFormats.NormalizeVehicleNumber(request.VehicleNumber), ownerId }, cancellationToken: cancellationToken);
        return id;
    }

    public async Task UpdateAsync(long vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await EnsureCanManageAsync(vehicleId, cancellationToken);
        await EnsureVehicleTypeAsync(request.VehicleTypeId, request.CapacityKg, cancellationToken);

        await _vehicles.UpdateAsync(new SaveVehicleCommand(
            vehicleId, vehicle.OwnerId, null, request.VehicleTypeId, request.Manufacturer.Trim(), request.Model.Trim(),
            request.ManufactureYear, request.CapacityKg, request.PermitNumber?.Trim(), request.PermitExpiryDate,
            request.InsuranceNumber?.Trim(), request.InsuranceExpiryDate, request.FitnessExpiryDate, request.PucExpiryDate,
            request.RowVersion, _access.User.UserId), cancellationToken);

        await _audit.LogAsync("VehicleUpdated", "Vehicle", vehicleId,
            oldValue: new { vehicle.VehicleTypeId, vehicle.CapacityKg, vehicle.InsuranceExpiryDate },
            newValue: new { request.VehicleTypeId, request.CapacityKg, request.InsuranceExpiryDate }, cancellationToken: cancellationToken);
    }

    public async Task SetAvailabilityAsync(long vehicleId, SetVehicleAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await EnsureCanManageAsync(vehicleId, cancellationToken);

        if (request.IsAvailable)
        {
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (vehicle.InsuranceExpiryDate is not null && vehicle.InsuranceExpiryDate < today)
            {
                throw new BusinessRuleException("INSURANCE_EXPIRED", "Renew the vehicle insurance before marking it available.");
            }
        }

        await _vehicles.SetAvailabilityAsync(vehicleId, request.IsAvailable, request.Reason, _access.User.UserId, cancellationToken);
    }

    public async Task VerifyAsync(long vehicleId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ApproveVehicles);
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken) ?? throw NotFoundException.For("Vehicle", vehicleId);

        if (request.Status == VerificationStatus.Verified)
        {
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (vehicle.InsuranceExpiryDate is null || vehicle.InsuranceExpiryDate < today)
            {
                throw new BusinessRuleException("INSURANCE_EXPIRED", "Valid insurance is required before verification.");
            }

            await _verification.EnsureMandatoryDocumentsVerifiedAsync(DocumentEntityType.Vehicle, vehicleId, cancellationToken);
        }

        await _vehicles.SetVerificationAsync(vehicleId, request.Status, request.Remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("VehicleVerification", "Vehicle", vehicleId, oldValue: new { vehicle.VerificationStatusId },
            newValue: new { Status = request.Status.ToString(), request.Remarks }, cancellationToken: cancellationToken);

        var owner = await _owners.GetByIdAsync(vehicle.OwnerId, cancellationToken);
        if (owner is not null)
        {
            await _notifications.NotifyUserAsync(owner.UserId, NotificationTemplates.VerificationUpdated,
                VerificationRules.NotificationData($"vehicle {vehicle.VehicleNumber}", new(request.Status, request.Remarks)),
                new NotificationSubject("Vehicle", vehicleId), cancellationToken);
        }
    }

    public async Task SetActiveAsync(long vehicleId, bool isActive, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(vehicleId, cancellationToken);
        await _vehicles.SetActiveAsync(vehicleId, isActive, _access.User.UserId, cancellationToken);
        await _audit.LogAsync(isActive ? "VehicleActivated" : "VehicleDeactivated", "Vehicle", vehicleId, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AvailableVehicleDto>> GetAvailableForAssignmentAsync(int vehicleTypeId, decimal minCapacityKg, DateTime onDateUtc,
        CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.AssignTrips);
        return _vehicles.GetAvailableForAssignmentAsync(vehicleTypeId, minCapacityKg, onDateUtc, cancellationToken);
    }

    public Task<IReadOnlyList<ExpiringVehicleDto>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(withinDays, 1, 365);
        if (_access.IsStaffWith(Permissions.ViewVehicles))
        {
            return _vehicles.GetExpiringAsync(days, null, cancellationToken);
        }

        return _vehicles.GetExpiringAsync(days, _access.RequireOwnerId(), cancellationToken);
    }

    private async Task<VehicleDto> EnsureCanManageAsync(long vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken) ?? throw NotFoundException.For("Vehicle", vehicleId);
        if (_access.IsStaffWith(Permissions.ManageVehicles)) return vehicle;
        if (_access.User.OwnerId == vehicle.OwnerId && _access.User.HasPermission(Permissions.ManageOwnFleet)) return vehicle;
        throw NotFoundException.For("Vehicle", vehicleId);
    }

    private async Task EnsureVehicleTypeAsync(int vehicleTypeId, decimal capacityKg, CancellationToken cancellationToken)
    {
        var type = (await _masterData.GetVehicleTypesAsync(false, cancellationToken)).FirstOrDefault(t => t.VehicleTypeId == vehicleTypeId)
                   ?? throw new BusinessRuleException("VEHICLE_TYPE_INVALID", "The selected vehicle type does not exist or is inactive.");

        // Registered capacity may exceed the nominal class capacity a little, never by a lot.
        if (capacityKg > type.CapacityKg * 1.5m)
        {
            throw new BusinessRuleException("CAPACITY_INCONSISTENT",
                $"Capacity {capacityKg:0} kg is not plausible for vehicle type '{type.Name}' ({type.CapacityKg:0} kg).");
        }
    }
}
