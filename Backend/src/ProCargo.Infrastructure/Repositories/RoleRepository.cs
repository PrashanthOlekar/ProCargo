using System.Text.Json;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    private readonly StoredProcedureExecutor _sp;

    public RoleRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<RoleDto>($"EXEC sec.usp_Role_GetAll", cancellationToken);

    public Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<PermissionDto>($"EXEC sec.usp_Role_GetPermissions @RoleId={roleId}", cancellationToken);

    public Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<PermissionDto>($"EXEC sec.usp_Permission_GetAll", cancellationToken);

    public async Task<int> CreateAsync(CreateRoleRequest request, long createdBy, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC sec.usp_Role_Create @Name={request.Name.Trim()}, @Description={request.Description}, @IsInternal={request.IsInternal},
                @CreatedBy={createdBy}
            """, cancellationToken);
        return (int)result.Id;
    }

    public Task UpdateAsync(int roleId, UpdateRoleRequest request, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_Role_Update @RoleId={roleId}, @Description={request.Description}, @IsActive={request.IsActive}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task SetPermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_Role_SetPermissions @RoleId={roleId}, @PermissionIdsJson={JsonSerializer.Serialize(permissionIds)}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task DeleteAsync(int roleId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_Role_Delete @RoleId={roleId}", cancellationToken);
}
