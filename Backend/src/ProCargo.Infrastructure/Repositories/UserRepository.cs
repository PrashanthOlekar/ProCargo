using System.Text.Json;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly StoredProcedureExecutor _sp;

    public UserRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<UserListItemDto>> GetPagedAsync(UserSearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "Name", "Email", "LastLogin");
        var rows = await _sp.QueryAsync<UserListItemDto>($"""
            EXEC sec.usp_User_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @IsInternal={request.IsInternal},
                @RoleId={request.RoleId}, @IsActive={request.IsActive}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<UserDetailsDto?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<UserDetailsDto>($"EXEC sec.usp_User_GetById @UserId={userId}", cancellationToken);

    public async Task<IReadOnlyList<long>> GetRoleIdsAsync(long userId, CancellationToken cancellationToken)
    {
        var rows = await _sp.QueryAsync<IntValue>($"EXEC sec.usp_User_GetRoleIds @UserId={userId}", cancellationToken);
        return rows.Select(r => (long)r.Value).ToList();
    }

    public async Task<long> CreateInternalAsync(CreateInternalUserCommand c, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC sec.usp_User_CreateInternal @Email={c.Email}, @NormalizedEmail={c.NormalizedEmail}, @PhoneNumber={c.PhoneNumber},
                @FullName={c.FullName}, @PasswordHash={c.PasswordHash}, @RoleIdsJson={JsonSerializer.Serialize(c.RoleIds)},
                @MustChangePassword={c.MustChangePassword}, @CreatedBy={c.CreatedBy}
            """, cancellationToken);
        return result.Id;
    }

    public Task UpdateAsync(long userId, UpdateUserRequest request, string normalizedPhone, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_User_Update @UserId={userId}, @FullName={request.FullName.Trim()}, @PhoneNumber={normalizedPhone},
                @ModifiedBy={modifiedBy}, @RowVersion={request.RowVersion}
            """, cancellationToken);

    public Task SetRolesAsync(long userId, IReadOnlyCollection<int> roleIds, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sec.usp_User_SetRoles @UserId={userId}, @RoleIdsJson={JsonSerializer.Serialize(roleIds)}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task SetActiveAsync(long userId, bool isActive, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_User_SetActive @UserId={userId}, @IsActive={isActive}, @ModifiedBy={modifiedBy}", cancellationToken);

    public Task UnlockAsync(long userId, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sec.usp_User_Unlock @UserId={userId}, @ModifiedBy={modifiedBy}", cancellationToken);

    internal sealed class IntValue
    {
        public int Value { get; init; }
    }
}
