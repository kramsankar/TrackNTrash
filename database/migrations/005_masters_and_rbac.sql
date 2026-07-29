/* =====================================================================================
   Migration 005 — Retail-chain master data + role-based access control.

   RETAIL HIERARCHY
   ----------------
   A retail chain moves stock from a distribution centre to its outlets. Physically the
   containment runs:

       Site (DC)  →  Zone  →  Rack  →  Tray  →  Carton  →  Item
       Store (outlet) is the destination the tray is despatched to.

   Racks are the new level: trays live in a rack position while they wait in the DC, so
   a picker can be told "tray TRAY-LDN1-000004 is in rack R-A12". ops.Tray therefore
   gains a nullable CurrentRackId.

   RBAC (mirrors the BMS pattern)
   ------------------------------
   Role  ×  Form  →  canCreate / canEdit / canDelete / canView

   Forms are the screens of the console. A user has one role; the role's mapping decides
   which screens they see and what they may do there. Admin short-circuits to full access.

   Idempotent. Run after 003.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ------------------------------------------------------------ Product master ---- */
IF OBJECT_ID(N'ops.Product', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Product
    (
        ProductId    INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
        Gtin         CHAR(14)      NOT NULL,
        Sku          NVARCHAR(40)  NULL,
        Name         NVARCHAR(200) NOT NULL,
        Category     NVARCHAR(80)  NULL,
        Brand        NVARCHAR(80)  NULL,
        UnitsPerCarton INT         NOT NULL CONSTRAINT DF_Product_UPC DEFAULT (1),
        ItemIdentification VARCHAR(10) NOT NULL CONSTRAINT DF_Product_Ident DEFAULT ('Visual'),
        Uom          NVARCHAR(10)  NOT NULL CONSTRAINT DF_Product_Uom DEFAULT ('EA'),
        IsActive     BIT           NOT NULL CONSTRAINT DF_Product_Active DEFAULT (1),
        CreatedUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Product_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Product_Gtin UNIQUE (Gtin)
    );
    PRINT 'Created ops.Product';
END
GO

/* ------------------------------------------------------------ Zone master ------- */
IF OBJECT_ID(N'ops.Zone', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Zone
    (
        ZoneId     INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Zone PRIMARY KEY,
        SiteCode   NVARCHAR(20)  NOT NULL,
        ZoneCode   NVARCHAR(30)  NOT NULL,
        Name       NVARCHAR(120) NOT NULL,
        ZoneType   VARCHAR(20)   NOT NULL CONSTRAINT DF_Zone_Type DEFAULT ('Storage'),
                   -- Storage | PickFace | Dispatch | GoodsIn | Staging
        IsActive   BIT           NOT NULL CONSTRAINT DF_Zone_Active DEFAULT (1),
        CreatedUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_Zone_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Zone_Code UNIQUE (SiteCode, ZoneCode)
    );
    PRINT 'Created ops.Zone';
END
GO

/* ------------------------------------------------------------ Rack master ------- */
IF OBJECT_ID(N'ops.Rack', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Rack
    (
        RackId     INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rack PRIMARY KEY,
        RackCode   NVARCHAR(30)  NOT NULL,          -- R-A12
        ZoneId     INT           NULL CONSTRAINT FK_Rack_Zone REFERENCES ops.Zone(ZoneId),
        SiteCode   NVARCHAR(20)  NOT NULL,
        Aisle      NVARCHAR(20)  NULL,
        [Level]    NVARCHAR(10)  NULL,              -- shelf level within the rack
        Capacity   INT           NOT NULL CONSTRAINT DF_Rack_Cap DEFAULT (10),   -- trays it holds
        IsActive   BIT           NOT NULL CONSTRAINT DF_Rack_Active DEFAULT (1),
        CreatedUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_Rack_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Rack_Code UNIQUE (SiteCode, RackCode)
    );
    PRINT 'Created ops.Rack';
END
GO

-- A tray waits in a rack position while it is in the DC.
IF COL_LENGTH('ops.Tray', 'CurrentRackId') IS NULL
BEGIN
    ALTER TABLE ops.Tray ADD CurrentRackId INT NULL
        CONSTRAINT FK_Tray_Rack REFERENCES ops.Rack(RackId);
    PRINT 'Added ops.Tray.CurrentRackId';
END
GO

/* ------------------------------------------------------------ RBAC -------------- */
IF OBJECT_ID(N'ops.Role', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Role
    (
        RoleId      INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
        RoleName    NVARCHAR(60)  NOT NULL,
        Description NVARCHAR(200) NULL,
        IsAdmin     BIT           NOT NULL CONSTRAINT DF_Role_Admin DEFAULT (0),
        IsActive    BIT           NOT NULL CONSTRAINT DF_Role_Active DEFAULT (1),
        CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Role_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Role_Name UNIQUE (RoleName)
    );
    PRINT 'Created ops.Role';
END
GO

-- The screens permissions are granted against.
IF OBJECT_ID(N'ops.AppForm', N'U') IS NULL
BEGIN
    CREATE TABLE ops.AppForm
    (
        FormId     NVARCHAR(40)  NOT NULL CONSTRAINT PK_AppForm PRIMARY KEY,   -- 'orders', 'assets'
        FormName   NVARCHAR(80)  NOT NULL,
        FormGroup  NVARCHAR(40)  NOT NULL,
        SortOrder  INT           NOT NULL CONSTRAINT DF_AppForm_Sort DEFAULT (100)
    );
    PRINT 'Created ops.AppForm';
END
GO

IF OBJECT_ID(N'ops.RoleFormMapping', N'U') IS NULL
BEGIN
    CREATE TABLE ops.RoleFormMapping
    (
        MappingId  INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoleFormMapping PRIMARY KEY,
        RoleId     INT          NOT NULL CONSTRAINT FK_RFM_Role REFERENCES ops.Role(RoleId),
        FormId     NVARCHAR(40) NOT NULL CONSTRAINT FK_RFM_Form REFERENCES ops.AppForm(FormId),
        CanView    BIT          NOT NULL CONSTRAINT DF_RFM_View   DEFAULT (0),
        CanCreate  BIT          NOT NULL CONSTRAINT DF_RFM_Create DEFAULT (0),
        CanEdit    BIT          NOT NULL CONSTRAINT DF_RFM_Edit   DEFAULT (0),
        CanDelete  BIT          NOT NULL CONSTRAINT DF_RFM_Delete DEFAULT (0),
        UpdatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_RFM_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_RFM UNIQUE (RoleId, FormId)
    );
    PRINT 'Created ops.RoleFormMapping';
END
GO

-- Users now carry a role reference (the legacy comma-separated Roles column stays for
-- backwards compatibility with tokens already issued).
IF COL_LENGTH('ops.AppUser', 'RoleId') IS NULL
BEGIN
    ALTER TABLE ops.AppUser ADD RoleId INT NULL CONSTRAINT FK_AppUser_Role REFERENCES ops.Role(RoleId);
    PRINT 'Added ops.AppUser.RoleId';
END
GO
IF COL_LENGTH('ops.AppUser', 'Email') IS NULL
BEGIN
    ALTER TABLE ops.AppUser ADD Email NVARCHAR(160) NULL;
    PRINT 'Added ops.AppUser.Email';
END
GO
IF COL_LENGTH('ops.AppUser', 'SiteCode') IS NULL
BEGIN
    ALTER TABLE ops.AppUser ADD SiteCode NVARCHAR(20) NULL;
    PRINT 'Added ops.AppUser.SiteCode';
END
GO

/* ------------------------------------------------------------ Seed -------------- */

-- Console screens.
MERGE ops.AppForm AS t
USING (VALUES
    ('dashboard','Dashboard','Overview',10),
    ('orders','Orders','Operations',20),
    ('trips','Trips & Loading','Operations',30),
    ('manifests','Manifests (ASN)','Operations',40),
    ('assets','Asset Master','Operations',50),
    ('lookup','Line Lookup','Operations',60),
    ('items','Item Counting','Inspection',70),
    ('cameras','Cameras & Map','Inspection',80),
    ('exceptions','Exceptions','Monitoring',90),
    ('m_product','Products','Masters',100),
    ('m_store','Stores','Masters',110),
    ('m_zone','Zones','Masters',120),
    ('m_rack','Racks','Masters',130),
    ('m_vehicle','Vehicles','Masters',140),
    ('m_device','Devices','Masters',150),
    ('m_role','Roles','Administration',200),
    ('m_user','Users','Administration',210),
    ('m_mapping','Role Mapping','Administration',220)
) AS s(FormId, FormName, FormGroup, SortOrder)
ON t.FormId = s.FormId
WHEN MATCHED THEN UPDATE SET FormName=s.FormName, FormGroup=s.FormGroup, SortOrder=s.SortOrder
WHEN NOT MATCHED THEN INSERT (FormId, FormName, FormGroup, SortOrder)
    VALUES (s.FormId, s.FormName, s.FormGroup, s.SortOrder);
GO

-- Baseline roles for a retail chain.
MERGE ops.Role AS t
USING (VALUES
    ('Admin','Full access to every screen and master',1),
    ('WarehouseManager','Runs the DC — operations, masters, exceptions',0),
    ('Dispatcher','Plans trips and monitors exceptions',0),
    ('Picker','Pick and tray build only',0),
    ('StoreManager','Receives deliveries for their own store',0)
) AS s(RoleName, Description, IsAdmin)
ON t.RoleName = s.RoleName
WHEN MATCHED THEN UPDATE SET Description=s.Description, IsAdmin=s.IsAdmin
WHEN NOT MATCHED THEN INSERT (RoleName, Description, IsAdmin) VALUES (s.RoleName, s.Description, s.IsAdmin);
GO

-- Sensible default permissions per role (Admin needs none — it short-circuits).
DECLARE @wm INT = (SELECT RoleId FROM ops.Role WHERE RoleName='WarehouseManager');
DECLARE @di INT = (SELECT RoleId FROM ops.Role WHERE RoleName='Dispatcher');
DECLARE @pk INT = (SELECT RoleId FROM ops.Role WHERE RoleName='Picker');
DECLARE @sm INT = (SELECT RoleId FROM ops.Role WHERE RoleName='StoreManager');

;WITH grants AS (
    SELECT * FROM (VALUES
        -- Warehouse manager: everything operational + masters, no admin screens
        (@wm,'dashboard',1,0,0,0),(@wm,'orders',1,1,1,0),(@wm,'trips',1,1,1,0),
        (@wm,'manifests',1,1,1,0),(@wm,'assets',1,1,1,1),(@wm,'lookup',1,0,0,0),
        (@wm,'items',1,1,1,0),(@wm,'cameras',1,1,1,0),(@wm,'exceptions',1,0,1,0),
        (@wm,'m_product',1,1,1,0),(@wm,'m_zone',1,1,1,0),(@wm,'m_rack',1,1,1,0),
        (@wm,'m_store',1,0,0,0),(@wm,'m_vehicle',1,1,1,0),(@wm,'m_device',1,1,1,0),
        -- Dispatcher: trips and monitoring
        (@di,'dashboard',1,0,0,0),(@di,'orders',1,0,0,0),(@di,'trips',1,1,1,0),
        (@di,'manifests',1,1,0,0),(@di,'lookup',1,0,0,0),(@di,'exceptions',1,0,1,0),
        (@di,'assets',1,0,0,0),
        -- Picker: pick and count only
        (@pk,'dashboard',1,0,0,0),(@pk,'orders',1,0,0,0),(@pk,'items',1,1,0,0),
        (@pk,'assets',1,0,0,0),(@pk,'lookup',1,0,0,0),
        -- Store manager: receiving view
        (@sm,'dashboard',1,0,0,0),(@sm,'lookup',1,0,0,0),(@sm,'items',1,1,0,0),
        (@sm,'exceptions',1,0,0,0)
    ) AS g(RoleId, FormId, CanView, CanCreate, CanEdit, CanDelete)
)
MERGE ops.RoleFormMapping AS t
USING grants AS s ON t.RoleId = s.RoleId AND t.FormId = s.FormId
WHEN MATCHED THEN UPDATE SET CanView=s.CanView, CanCreate=s.CanCreate, CanEdit=s.CanEdit, CanDelete=s.CanDelete
WHEN NOT MATCHED THEN INSERT (RoleId, FormId, CanView, CanCreate, CanEdit, CanDelete)
    VALUES (s.RoleId, s.FormId, s.CanView, s.CanCreate, s.CanEdit, s.CanDelete);
GO

-- Point existing users at the matching role.
UPDATE u SET RoleId = r.RoleId
FROM ops.AppUser u JOIN ops.Role r ON u.Roles LIKE '%' + r.RoleName + '%'
WHERE u.RoleId IS NULL;
GO

PRINT 'Migration 005 complete.';
GO
