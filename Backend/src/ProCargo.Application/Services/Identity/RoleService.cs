using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Services.Identity;

/// <summary>Role and permission management. New roles can be added without code changes.</summary>
public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roles;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public RoleService(IRoleRepository roles, ICurrentUser currentUser, IAuditLogger audit)
    {
        _roles = roles;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken) => _roles.GetAllAsync(cancellationToken);

    public async Task<RoleDetailsResponse> GetByIdAsync(int roleId, CancellationToken cancellationToken)
    {
        var role = (await _roles.GetAllAsync(cancellationToken)).FirstOrDefault(r => r.RoleId == roleId)
                   ?? throw NotFoundException.For("Role", roleId);
        var permissions = await _roles.GetPermissionsAsync(roleId, cancellationToken);
        return new RoleDetailsResponse(role, permissions);
    }

    public Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken) =>
        _roles.GetAllPermissionsAsync(cancellationToken);

    public async Task<int> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var roleId = await _roles.CreateAsync(request, _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("RoleCreated", "Role", roleId, newValue: new { request.Name, request.IsInternal }, cancellationToken: cancellationToken);
        return roleId;
    }

    public async Task UpdateAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        await _roles.UpdateAsync(roleId, request, _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("RoleUpdated", "Role", roleId, newValue: request, cancellationToken: cancellationToken);
    }

    public async Task SetPermissionsAsync(int roleId, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var before = await _roles.GetPermissionsAsync(roleId, cancellationToken);
        await _roles.SetPermissionsAsync(roleId, request.PermissionIds.Distinct().ToArray(), _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("RolePermissionsChanged", "Role", roleId,
            oldValue: before.Select(p => p.Code), newValue: request.PermissionIds, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken)
    {
        await _roles.DeleteAsync(roleId, cancellationToken);
        await _audit.LogAsync("RoleDeleted", "Role", roleId, cancellationToken: cancellationToken);
    }
}
