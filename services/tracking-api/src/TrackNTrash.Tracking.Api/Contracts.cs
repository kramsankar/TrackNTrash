using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Infrastructure;

namespace TrackNTrash.Tracking.Api;

/// <summary>Inbound scan/verification event (POST /events/scan).</summary>
public sealed record ScanEventDto
{
    public string ClientEventId { get; init; } = "";
    public string EventType { get; init; } = "";
    public string? Checkpoint { get; init; }
    public string DeviceId { get; init; } = "";
    public string? UserId { get; init; }
    public string? ScannedQr { get; init; }
    public long? OrderLineId { get; init; }
    public string? OrderLineRef { get; init; }
    public long? CartonId { get; init; }
    public int? TrayId { get; init; }
    public string? TrayQr { get; init; }
    public long? TripId { get; init; }
    public int? StoreId { get; init; }
    public string? Verdict { get; init; }
    public string? Meta { get; init; }
    public DateTimeOffset? EventUtc { get; init; }

    public ScanEventInput ToInput() => new()
    {
        ClientEventId = ClientEventId,
        EventType = EventType,
        Checkpoint = Checkpoint,
        DeviceId = DeviceId,
        UserId = UserId,
        ScannedQr = ScannedQr,
        OrderLineId = OrderLineId,
        OrderLineRef = OrderLineRef,
        CartonId = CartonId,
        TrayId = TrayId,
        TrayQr = TrayQr,
        TripId = TripId,
        StoreId = StoreId,
        Verdict = Verdict,
        MetaJson = Meta,
        EventUtc = EventUtc ?? DateTimeOffset.UtcNow
    };
}

/// <summary>Order intake (POST /orders) — creates SalesOrder + OrderLine master data.</summary>
public sealed record OrderDto
{
    public string OrderNumber { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public string? ErpReference { get; init; }
    public List<OrderLineDto> Lines { get; init; } = new();

    public SqlOrderStore.OrderInput ToInput() => new(
        OrderNumber, StoreCode, ErpReference,
        Lines.Select(l => new SqlOrderStore.OrderLineInput(
            l.LineNumber, l.Gtin, l.OrderedQty, l.Uom, l.ExpectedCartonCount, l.ErpLineReference)).ToList());
}

public sealed record OrderLineDto
{
    public int LineNumber { get; init; }
    public string Gtin { get; init; } = "";
    public decimal OrderedQty { get; init; }
    public string Uom { get; init; } = "EA";
    public int ExpectedCartonCount { get; init; }
    public string? ErpLineReference { get; init; }
}

// ---------- RBAC ----------

/// <summary>Save one role × form permission row (POST /rbac/mappings).</summary>
public sealed record MappingDto
{
    public int RoleId { get; init; }
    public string FormId { get; init; } = "";
    public bool CanView { get; init; }
    public bool CanCreate { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}

/// <summary>Create or update a user (POST /rbac/users). Blank password keeps the existing one.</summary>
public sealed record SaveUserDto
{
    public int? UserId { get; init; }
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Email { get; init; }
    public int? RoleId { get; init; }
    public string? SiteCode { get; init; }
    public string? Password { get; init; }
    public bool IsActive { get; init; } = true;
}

// ---------- Item-level tracking ----------

/// <summary>Create/define a carton and how its units are identified (POST /cartons).</summary>
public sealed record CartonSetupDto
{
    public long OrderLineId { get; init; } = 1;
    public string Gtin { get; init; } = "";
    public string Serial { get; init; } = "";
    public int ExpectedItemCount { get; init; }
    /// <summary>Barcoded | Visual | Mixed</summary>
    public string ItemIdentification { get; init; } = "Visual";
    /// <summary>Optional barcoded units to register up front.</summary>
    public List<ItemDto> Items { get; init; } = new();
}

public sealed record ItemDto
{
    public string Barcode { get; init; } = "";
    public string? Gtin { get; init; }
    public string? Description { get; init; }
}

/// <summary>Record an item-level observation of a carton (POST /items/count).</summary>
public sealed record ItemCountDto
{
    public long CartonId { get; init; }
    /// <summary>PickTrayBuild | DispatchDock | StoreReceive</summary>
    public string? Checkpoint { get; init; }
    /// <summary>Barcodes actually scanned (empty for purely visual counting).</summary>
    public List<string> ScannedBarcodes { get; init; } = new();
    /// <summary>Units counted by a camera (null when no camera observed).</summary>
    public int? VisionCount { get; init; }
    public int? CameraId { get; init; }
    public string? FrameBlobUri { get; init; }
    public decimal? Confidence { get; init; }
    public string DeviceId { get; init; } = "console";
}

// ---------- Cameras ----------

public sealed record CameraDto
{
    public string CameraCode { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Fixed | Handheld</summary>
    public string CameraKind { get; init; } = "Fixed";
    public string SiteCode { get; init; } = "";
    public string? Zone { get; init; }
    public string? Station { get; init; }
    public string? Checkpoint { get; init; }
    public string? RtspUrl { get; init; }
    /// <summary>ItemCount | CartonVerify | Both</summary>
    public string Purpose { get; init; } = "ItemCount";
    public string Status { get; init; } = "Active";
}

/// <summary>Pin a camera onto a site map (POST /cameras/{id}/placement).</summary>
public sealed record PlacementDto
{
    public int SiteMapId { get; init; }
    public decimal X { get; init; }
    public decimal Y { get; init; }
    public int? HeadingDeg { get; init; }
}

public sealed record SiteMapDto
{
    public string SiteCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string? ImageUri { get; init; }
    public int Width { get; init; } = 1000;
    public int Height { get; init; } = 600;
}

/// <summary>Username/password sign-in (POST /auth/login).</summary>
public sealed record LoginDto
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}

/// <summary>Create/update a local user (POST /auth/users, guarded by the setup key).</summary>
public sealed record UpsertUserDto
{
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Roles { get; init; } = "Dispatcher";
}

/// <summary>Register N reusable trays for a site (POST /assets/register).</summary>
public sealed record RegisterAssetsDto
{
    public string SiteCode { get; init; } = "";
    public int Count { get; init; } = 1;
}

/// <summary>Manifest upsert (PUT /manifests) — normally driven by trip planning / D365.</summary>
public sealed record ManifestDto
{
    public string TrayQr { get; init; } = "";
    public long? TripId { get; init; }
    public int ExpectedCartonCount { get; init; }
    public List<string> ExpectedCartonPayloads { get; init; } = new();

    public TrayManifest ToManifest() => new()
    {
        TrayQr = TrayQr,
        TripId = TripId,
        ExpectedCartonCount = ExpectedCartonCount,
        ExpectedCartonPayloads = ExpectedCartonPayloads,
        UpdatedUtc = DateTimeOffset.UtcNow
    };
}
