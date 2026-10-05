-- Run once per environment, connected to the ProCargo database as a member of the SQL Entra admin group
-- (Entra-only authentication is enabled, so there are no SQL logins or passwords).
-- Replace the name with the API web app name printed by the deployment (output apiAppName).

DECLARE @app sysname = N'app-procargo-prod-api-xxxxxx';
DECLARE @sql nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @app)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@app) + N' FROM EXTERNAL PROVIDER;';
    EXEC (@sql);
END;

-- The API only executes stored procedures; it is given no direct table access.
-- Role procargo_app is created by Database/ProCargo.Database/11-Security.
SET @sql = N'ALTER ROLE procargo_app ADD MEMBER ' + QUOTENAME(@app) + N';';
EXEC (@sql);
