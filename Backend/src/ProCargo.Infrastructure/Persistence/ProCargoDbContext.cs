using Microsoft.EntityFrameworkCore;

namespace ProCargo.Infrastructure.Persistence;

/// <summary>
/// EF Core context used for connection management, transactions, parameterised stored procedure execution
/// and result mapping. It deliberately has no DbSets: all business CRUD goes through stored procedures
/// (see <see cref="StoredProcedureExecutor"/>), and the schema is owned by ProCargo.Database scripts,
/// not EF migrations.
/// </summary>
public sealed class ProCargoDbContext : DbContext
{
    public ProCargoDbContext(DbContextOptions<ProCargoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProCargoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Result-mapping conventions for the DTOs returned by stored procedures.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 6);
    }
}
