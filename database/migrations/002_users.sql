/* =====================================================================================
   Migration 002 — Application users for username/password sign-in.
   Entra ID users are NOT stored here (they authenticate against Azure AD); this table
   backs the local login used on shared warehouse devices.
   Passwords are PBKDF2-HMAC-SHA256, 100k iterations, stored as base64(salt):base64(hash).
   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'ops.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE ops.AppUser
    (
        UserId       INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
        Username     NVARCHAR(80)  NOT NULL,
        DisplayName  NVARCHAR(120) NOT NULL,
        PasswordHash NVARCHAR(200) NOT NULL,   -- base64(salt):base64(hash)
        Roles        NVARCHAR(200) NOT NULL,   -- comma-separated: Admin,WarehouseManager,Dispatcher
        IsActive     BIT           NOT NULL CONSTRAINT DF_AppUser_Active DEFAULT (1),
        LastLoginUtc DATETIME2(3)  NULL,
        CreatedUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_AppUser_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AppUser_Username UNIQUE (Username)
    );
    PRINT 'Created ops.AppUser';
END
ELSE PRINT 'ops.AppUser already exists';
GO
