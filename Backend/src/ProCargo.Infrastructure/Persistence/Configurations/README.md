# Persistence/Configurations

`IEntityTypeConfiguration<T>` classes go here if a table is ever mapped as an EF entity
(e.g. for a read-only reporting projection). Today the API maps **no** tables: every business
read/write is a stored procedure call (`StoredProcedureExecutor`), and the result rows are
mapped to plain DTO classes with `Database.SqlQuery<T>()`.

`ProCargoDbContext.OnModelCreating` already calls `ApplyConfigurationsFromAssembly`, so adding a
configuration class here is all that is needed.
