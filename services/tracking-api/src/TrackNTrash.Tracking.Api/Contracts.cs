using TrackNTrash.Tracking.Core;

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

    public Infrastructure.SqlOrderStore.OrderInput ToInput() => new(
        OrderNumber, StoreCode, ErpReference,
        Lines.Select(l => new Infrastructure.SqlOrderStore.OrderLineInput(
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
