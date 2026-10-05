using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ProCargo.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over EF Core raw SQL APIs used by every repository.
///
///   * Calls are written as interpolated strings: <c>$"EXEC core.usp_Booking_GetById @BookingId={id}"</c>.
///     EF Core turns every interpolated value into a DbParameter, so user input is never concatenated into SQL.
///   * Results are materialised into DTO classes whose properties match the procedure's columns.
///   * SQL errors raised by procedures (THROW 504xx) are translated into typed application exceptions.
///
/// Note: stored procedure calls are not composable, so results are always materialised with ToListAsync
/// before any LINQ is applied.
/// </summary>
internal sealed class StoredProcedureExecutor
{
    private readonly ProCargoDbContext _db;

    public StoredProcedureExecutor(ProCargoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(FormattableString sql, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Database.SqlQuery<T>(sql).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (SqlErrorTranslator.TryTranslate(ex, out var translated))
        {
            throw translated!;
        }
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(FormattableString sql, CancellationToken cancellationToken)
        where T : class
    {
        var rows = await QueryAsync<T>(sql, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<T> QuerySingleAsync<T>(FormattableString sql, CancellationToken cancellationToken)
        where T : class
    {
        var rows = await QueryAsync<T>(sql, cancellationToken);
        return rows.Count > 0
            ? rows[0]
            : throw new InvalidOperationException($"Stored procedure returned no rows: {sql.Format}");
    }

    /// <summary>Single scalar result. The procedure must name its column "Value" (EF Core convention).</summary>
    public async Task<T> QueryScalarAsync<T>(FormattableString sql, CancellationToken cancellationToken)
        where T : struct
    {
        var rows = await QueryAsync<T>(sql, cancellationToken);
        return rows.Count > 0 ? rows[0] : default;
    }

    public async Task ExecuteAsync(FormattableString sql, CancellationToken cancellationToken)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
        }
        catch (Exception ex) when (SqlErrorTranslator.TryTranslate(ex, out var translated))
        {
            throw translated!;
        }
    }
}

/// <summary>
/// Explicitly typed parameters for values whose precision must be preserved. EF Core's default decimal
/// parameter mapping is decimal(18,2), which would round coordinates (decimal(9,6)) and 4-decimal rates.
/// </summary>
internal static class SqlParams
{
    public static SqlParameter Decimal(string name, decimal? value, byte precision, byte scale) =>
        new("dp_" + name, SqlDbType.Decimal)
        {
            Precision = precision,
            Scale = scale,
            Value = value.HasValue ? value.Value : DBNull.Value
        };

    public static SqlParameter Coordinate(string name, decimal? value) => Decimal(name, value, 9, 6);

    public static SqlParameter Json(string name, string json) =>
        new("dp_" + name, SqlDbType.NVarChar, -1) { Value = json };
}
