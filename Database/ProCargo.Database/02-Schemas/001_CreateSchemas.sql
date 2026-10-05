/*
    Schemas group objects by bounded context. They give us:
      * security boundaries: the application login is granted EXECUTE per schema and
        has NO direct table access (see 11-Security),
      * readability: every object name says which module owns it,
      * independent evolution of modules (e.g. moving fin to its own database later).

      sec  - identity, roles, permissions, tokens, login history
      mst  - master/reference data and system settings
      core - customers, owners, drivers, vehicles, bookings, quotations, trips, tracking, POD, files
      fin  - pricing, invoices, payments, refunds, settlements, commission
      sup  - complaints, support tickets, enquiries, notifications
      aud  - audit log
      rpt  - reporting views and report/dashboard procedures
*/
IF SCHEMA_ID(N'sec')  IS NULL EXEC (N'CREATE SCHEMA sec AUTHORIZATION dbo');
IF SCHEMA_ID(N'mst')  IS NULL EXEC (N'CREATE SCHEMA mst AUTHORIZATION dbo');
IF SCHEMA_ID(N'core') IS NULL EXEC (N'CREATE SCHEMA core AUTHORIZATION dbo');
IF SCHEMA_ID(N'fin')  IS NULL EXEC (N'CREATE SCHEMA fin AUTHORIZATION dbo');
IF SCHEMA_ID(N'sup')  IS NULL EXEC (N'CREATE SCHEMA sup AUTHORIZATION dbo');
IF SCHEMA_ID(N'aud')  IS NULL EXEC (N'CREATE SCHEMA aud AUTHORIZATION dbo');
IF SCHEMA_ID(N'rpt')  IS NULL EXEC (N'CREATE SCHEMA rpt AUTHORIZATION dbo');
GO
