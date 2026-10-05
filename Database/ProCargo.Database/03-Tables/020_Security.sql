/*
    Identity & security tables.
    Passwords are stored as ASP.NET Core Identity v3 hashes (PBKDF2-HMAC-SHA512).
    Refresh tokens and password reset tokens are stored as SHA-256 hashes only - never raw values.
*/

IF OBJECT_ID(N'sec.[User]', N'U') IS NULL
BEGIN
    CREATE TABLE sec.[User]
    (
        UserId              BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_User PRIMARY KEY,
        Email               NVARCHAR(256)   NOT NULL,
        NormalizedEmail     NVARCHAR(256)   NOT NULL,
        PhoneNumber         VARCHAR(20)     NOT NULL,
        FullName            NVARCHAR(150)   NOT NULL,
        PasswordHash        NVARCHAR(500)   NOT NULL,
        SecurityStamp       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_User_SecurityStamp DEFAULT (NEWID()),
        EmailConfirmed      BIT             NOT NULL CONSTRAINT DF_User_EmailConfirmed DEFAULT (0),
        PhoneConfirmed      BIT             NOT NULL CONSTRAINT DF_User_PhoneConfirmed DEFAULT (0),
        IsInternal          BIT             NOT NULL CONSTRAINT DF_User_IsInternal DEFAULT (0),
        FailedLoginCount    INT             NOT NULL CONSTRAINT DF_User_FailedLoginCount DEFAULT (0),
        LockoutEndUtc       DATETIME2(3)    NULL,
        LastLoginDateUtc    DATETIME2(3)    NULL,
        MustChangePassword  BIT             NOT NULL CONSTRAINT DF_User_MustChangePassword DEFAULT (0),
        IsActive            BIT             NOT NULL CONSTRAINT DF_User_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_User_IsDeleted DEFAULT (0),
        CreatedBy           BIGINT          NULL,
        CreatedDateUtc      DATETIME2(3)    NOT NULL CONSTRAINT DF_User_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy          BIGINT          NULL,
        ModifiedDateUtc     DATETIME2(3)    NULL,
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT CK_User_FailedLoginCount CHECK (FailedLoginCount >= 0),
        CONSTRAINT CK_User_PhoneNumber CHECK (PhoneNumber NOT LIKE '%[^0-9+]%' AND LEN(PhoneNumber) BETWEEN 10 AND 15)
    );
END
GO

IF OBJECT_ID(N'sec.Role', N'U') IS NULL
BEGIN
    CREATE TABLE sec.Role
    (
        RoleId           INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
        Name             VARCHAR(50)    NOT NULL CONSTRAINT UQ_Role_Name UNIQUE,
        Description      NVARCHAR(200)  NULL,
        IsSystem         BIT            NOT NULL CONSTRAINT DF_Role_IsSystem DEFAULT (0),
        IsInternal       BIT            NOT NULL CONSTRAINT DF_Role_IsInternal DEFAULT (1),
        IsActive         BIT            NOT NULL CONSTRAINT DF_Role_IsActive DEFAULT (1),
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_Role_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'sec.Permission', N'U') IS NULL
BEGIN
    CREATE TABLE sec.Permission
    (
        PermissionId     INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permission PRIMARY KEY,
        Code             VARCHAR(80)    NOT NULL CONSTRAINT UQ_Permission_Code UNIQUE,
        Name             NVARCHAR(150)  NOT NULL,
        Module           VARCHAR(50)    NOT NULL,
        IsInternal       BIT            NOT NULL CONSTRAINT DF_Permission_IsInternal DEFAULT (1)
    );
END
GO

IF OBJECT_ID(N'sec.RolePermission', N'U') IS NULL
BEGIN
    CREATE TABLE sec.RolePermission
    (
        RoleId           INT            NOT NULL,
        PermissionId     INT            NOT NULL,
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_RolePermission_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RolePermission PRIMARY KEY (RoleId, PermissionId)
    );
END
GO

IF OBJECT_ID(N'sec.UserRole', N'U') IS NULL
BEGIN
    CREATE TABLE sec.UserRole
    (
        UserId           BIGINT         NOT NULL,
        RoleId           INT            NOT NULL,
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_UserRole_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UserRole PRIMARY KEY (UserId, RoleId)
    );
END
GO

IF OBJECT_ID(N'sec.RefreshToken', N'U') IS NULL
BEGIN
    CREATE TABLE sec.RefreshToken
    (
        RefreshTokenId     BIGINT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
        UserId             BIGINT           NOT NULL,
        TokenHash          BINARY(32)       NOT NULL CONSTRAINT UQ_RefreshToken_TokenHash UNIQUE,
        FamilyId           UNIQUEIDENTIFIER NOT NULL,
        Portal             VARCHAR(20)      NOT NULL CONSTRAINT CK_RefreshToken_Portal CHECK (Portal IN ('Web','Operations')),
        ExpiresDateUtc     DATETIME2(3)     NOT NULL,
        CreatedDateUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_RefreshToken_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByIp        VARCHAR(45)      NULL,
        UserAgent          NVARCHAR(300)    NULL,
        RevokedDateUtc     DATETIME2(3)     NULL,
        RevokedReason      VARCHAR(50)      NULL,
        ReplacedByTokenId  BIGINT           NULL
    );
END
GO

IF OBJECT_ID(N'sec.PasswordResetToken', N'U') IS NULL
BEGIN
    CREATE TABLE sec.PasswordResetToken
    (
        PasswordResetTokenId BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetToken PRIMARY KEY,
        UserId               BIGINT        NOT NULL,
        TokenHash            BINARY(32)    NOT NULL CONSTRAINT UQ_PasswordResetToken_TokenHash UNIQUE,
        ExpiresDateUtc       DATETIME2(3)  NOT NULL,
        UsedDateUtc          DATETIME2(3)  NULL,
        RequestedIp          VARCHAR(45)   NULL,
        CreatedDateUtc       DATETIME2(3)  NOT NULL CONSTRAINT DF_PasswordResetToken_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'sec.LoginHistory', N'U') IS NULL
BEGIN
    CREATE TABLE sec.LoginHistory
    (
        LoginHistoryId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginHistory PRIMARY KEY,
        UserId           BIGINT         NULL,
        AttemptedEmail   NVARCHAR(256)  NOT NULL,
        IsSuccess        BIT            NOT NULL,
        FailureReason    VARCHAR(50)    NULL,
        Portal           VARCHAR(20)    NOT NULL,
        IpAddress        VARCHAR(45)    NULL,
        UserAgent        NVARCHAR(300)  NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_LoginHistory_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
