using Microsoft.EntityFrameworkCore;
using ProCargo.Application.Interfaces.Services;

namespace ProCargo.Infrastructure.Persistence;

/// <summary>
/// Runs a unit of work in one EF Core transaction. The retrying execution strategy requires
/// user-initiated transactions to be wrapped in strategy.ExecuteAsync so the whole unit is retried
/// together on a transient failure. Nested calls join the existing transaction.
/// </summary>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ProCargoDbContext _db;

    public EfUnitOfWork(ProCargoDbContext db)
    {
        _db = db;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
