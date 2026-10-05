/*
    ProCargo Logistics - Database creation
    Run against the [master] database. All later scripts run against [ProCargo].
    Collation: case-insensitive, accent-sensitive Latin collation (default for Indian English locales).
*/
IF DB_ID(N'ProCargo') IS NULL
BEGIN
    CREATE DATABASE [ProCargo] COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

ALTER DATABASE [ProCargo] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
ALTER DATABASE [ProCargo] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO
ALTER DATABASE [ProCargo] SET RECOVERY FULL;
GO
