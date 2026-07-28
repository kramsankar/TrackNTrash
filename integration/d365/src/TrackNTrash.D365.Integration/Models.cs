namespace TrackNTrash.D365.Integration;

// ================= Outbound from F&O (business events) =================

/// <summary>F&O business event: a sales order + warehouse work has been confirmed/released.</summary>
public sealed record FoSalesOrderConfirmed
{
    public string EventId { get; init; } = "";            // F&O BusinessEventId — idempotency key
    public string SalesOrderNumber { get; init; } = "";
    public string CustomerAccount { get; init; } = "";    // maps to store
    public string StoreCode { get; init; } = "";
    public DateTimeOffset OrderDate { get; init; }
    public DateTimeOffset? RequestedShipDate { get; init; }
    public IReadOnlyList<FoOrderLine> Lines { get; init; } = Array.Empty<FoOrderLine>();
}

public sealed record FoOrderLine
{
    public int LineNumber { get; init; }
    public string ItemId { get; init; } = "";
    public string Gtin { get; init; } = "";
    public decimal Quantity { get; init; }
    public string Uom { get; init; } = "EA";
    public int ExpectedCartonCount { get; init; }
    public string InventTransId { get; init; } = "";      // ties back to WHSWorkLine for pick confirm
}

/// <summary>Order-intake payload posted into the tracking system (creates order/lines/expected cartons).</summary>
public sealed record OrderIntake
{
    public string OrderNumber { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public string ErpReference { get; init; } = "";
    public DateTimeOffset OrderDate { get; init; }
    public DateTimeOffset? RequestedDeliveryDate { get; init; }
    public IReadOnlyList<OrderIntakeLine> Lines { get; init; } = Array.Empty<OrderIntakeLine>();
}

public sealed record OrderIntakeLine
{
    public int LineNumber { get; init; }
    public string Gtin { get; init; } = "";
    public decimal OrderedQty { get; init; }
    public string Uom { get; init; } = "EA";
    public int ExpectedCartonCount { get; init; }
    public string ErpLineReference { get; init; } = "";
}

// ================= Inbound to F&O (from tracking events) =================

public enum TrackingEventKind { TrayBuildComplete, ShipmentConfirmed, ReceivingComplete }

/// <summary>A tracking-system event that must post back to F&O.</summary>
public sealed record TrackingOutboundEvent
{
    public string EventId { get; init; } = "";            // tracking ScanEventId / correlation — idempotency key
    public TrackingEventKind Kind { get; init; }
    public string OrderNumber { get; init; } = "";
    public string? ErpReference { get; init; }
    public string? InventTransId { get; init; }
    public string? TripNumber { get; init; }
    public string? TrayQr { get; init; }
    public string StoreCode { get; init; } = "";
    public IReadOnlyList<LineResult> Lines { get; init; } = Array.Empty<LineResult>();
}

public sealed record LineResult
{
    public int LineNumber { get; init; }
    public string Gtin { get; init; } = "";
    public decimal ExpectedQty { get; init; }
    public decimal ReceivedQty { get; init; }
    public bool Short => ReceivedQty < ExpectedQty;
}

// ================= F&O posting request shapes =================

public sealed record PickingConfirmation
{
    public string OrderNumber { get; init; } = "";
    public string InventTransId { get; init; } = "";
    public decimal QtyPicked { get; init; }
}

public sealed record ShipmentConfirmation
{
    public string OrderNumber { get; init; } = "";
    public string TripNumber { get; init; } = "";
    public DateTimeOffset ShipDate { get; init; }
}

public sealed record DeliveryNotePosting
{
    public string OrderNumber { get; init; } = "";
    public DateTimeOffset DeliveryDate { get; init; }
    public IReadOnlyList<DeliveryLine> Lines { get; init; } = Array.Empty<DeliveryLine>();
    public bool HasShortages => Lines.Any(l => l.ShortQty > 0);
}

public sealed record DeliveryLine
{
    public int LineNumber { get; init; }
    public string Gtin { get; init; } = "";
    public decimal DeliveredQty { get; init; }
    public decimal ShortQty { get; init; }
}

/// <summary>How shortages at receiving are handled in F&O.</summary>
public enum ShortageHandling { QuantityAdjustment, CreateCase }
