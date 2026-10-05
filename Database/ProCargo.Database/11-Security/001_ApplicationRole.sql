/*
    Least-privilege database access for ProCargo.API.

    The API connects as a user that is a member of [procargo_app] ONLY. That role can EXECUTE stored
    procedures in the application schemas and nothing else: no direct SELECT/INSERT/UPDATE/DELETE on
    tables. Even a bug that tried to run ad-hoc SQL against a table would be refused by SQL Server.

    Ownership chaining (all objects owned by dbo) lets procedures read/write tables on the caller's behalf.
*/
IF DATABASE_PRINCIPAL_ID(N'procargo_app') IS NULL
    CREATE ROLE procargo_app AUTHORIZATION dbo;
GO

GRANT EXECUTE ON SCHEMA::sec  TO procargo_app;
GRANT EXECUTE ON SCHEMA::mst  TO procargo_app;
GRANT EXECUTE ON SCHEMA::core TO procargo_app;
GRANT EXECUTE ON SCHEMA::fin  TO procargo_app;
GRANT EXECUTE ON SCHEMA::sup  TO procargo_app;
GRANT EXECUTE ON SCHEMA::aud  TO procargo_app;
GRANT EXECUTE ON SCHEMA::rpt  TO procargo_app;
GO

DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::sec  TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::mst  TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::core TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::fin  TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::sup  TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::aud  TO procargo_app;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::rpt  TO procargo_app;
GO

/*
    Create the application principal per environment (never commit real passwords):

    -- Azure SQL with the API's managed identity (recommended, no password at all):
    CREATE USER [app-procargo-api-prod] FROM EXTERNAL PROVIDER;
    ALTER ROLE procargo_app ADD MEMBER [app-procargo-api-prod];

    -- SQL Server with SQL authentication (local / on-premises):
    CREATE LOGIN procargo_api WITH PASSWORD = '<from secret store>', CHECK_POLICY = ON;
    CREATE USER procargo_api FOR LOGIN procargo_api;
    ALTER ROLE procargo_app ADD MEMBER procargo_api;
*/
