namespace TrackNTrash.D365.Integration;

/// <summary>Pure mapping between F&O shapes and tracking-system shapes (see mapping.md).</summary>
public static class Mapping
{
    // ---- Outbound from F&O → tracking order intake ----
    public static OrderIntake ToOrderIntake(FoSalesOrderConfirmed e) => new()
    {
        OrderNumber = e.SalesOrderNumber,
        StoreCode = string.IsNullOrWhiteSpace(e.StoreCode) ? e.CustomerAccount : e.StoreCode,
        ErpReference = e.SalesOrderNumber,
        OrderDate = e.OrderDate,
        RequestedDeliveryDate = e.RequestedShipDate,
        Lines = e.Lines.Select(l => new OrderIntakeLine
        {
            LineNumber = l.LineNumber,
            Gtin = l.Gtin,
            OrderedQty = l.Quantity,
            Uom = l.Uom,
            ExpectedCartonCount = l.ExpectedCartonCount,
            ErpLineReference = l.InventTransId
        }).ToList()
    };

    // ---- Inbound to F&O ----
    public static PickingConfirmation ToPickingConfirmation(TrackingOutboundEvent e)
    {
        var line = e.Lines.FirstOrDefault();
        return new PickingConfirmation
        {
            OrderNumber = e.OrderNumber,
            InventTransId = e.InventTransId ?? "",
            QtyPicked = line?.ReceivedQty ?? line?.ExpectedQty ?? 0
        };
    }

    public static ShipmentConfirmation ToShipmentConfirmation(TrackingOutboundEvent e, DateTimeOffset shipDate) => new()
    {
        OrderNumber = e.OrderNumber,
        TripNumber = e.TripNumber ?? "",
        ShipDate = shipDate
    };

    public static DeliveryNotePosting ToDeliveryNote(TrackingOutboundEvent e, DateTimeOffset deliveryDate) => new()
    {
        OrderNumber = e.OrderNumber,
        DeliveryDate = deliveryDate,
        Lines = e.Lines.Select(l => new DeliveryLine
        {
            LineNumber = l.LineNumber,
            Gtin = l.Gtin,
            DeliveredQty = l.ReceivedQty,
            ShortQty = Math.Max(0, l.ExpectedQty - l.ReceivedQty)
        }).ToList()
    };
}
