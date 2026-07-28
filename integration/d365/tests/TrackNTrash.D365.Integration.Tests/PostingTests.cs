using Microsoft.Extensions.Logging.Abstractions;
using TrackNTrash.D365.Integration;
using Xunit;

namespace TrackNTrash.D365.Integration.Tests;

public class MappingTests
{
    [Fact]
    public void FoSalesOrder_maps_to_order_intake()
    {
        var e = new FoSalesOrderConfirmed
        {
            EventId = "be-1", SalesOrderNumber = "SO1001", CustomerAccount = "C1", StoreCode = "S-101",
            OrderDate = DateTimeOffset.UtcNow,
            Lines = new[] { new FoOrderLine { LineNumber = 1, Gtin = "09501234567891", Quantity = 10, ExpectedCartonCount = 2, InventTransId = "IT-1" } }
        };

        var intake = Mapping.ToOrderIntake(e);
        Assert.Equal("SO1001", intake.OrderNumber);
        Assert.Equal("S-101", intake.StoreCode);       // store code preferred over customer account
        var line = Assert.Single(intake.Lines);
        Assert.Equal(2, line.ExpectedCartonCount);
        Assert.Equal("IT-1", line.ErpLineReference);
    }

    [Fact]
    public void Store_falls_back_to_customer_account()
    {
        var e = new FoSalesOrderConfirmed { SalesOrderNumber = "SO2", CustomerAccount = "C9", StoreCode = "" };
        Assert.Equal("C9", Mapping.ToOrderIntake(e).StoreCode);
    }

    [Fact]
    public void Receiving_maps_to_delivery_note_with_shortages()
    {
        var e = new TrackingOutboundEvent
        {
            EventId = "ev1", Kind = TrackingEventKind.ReceivingComplete, OrderNumber = "SO1001",
            Lines = new[]
            {
                new LineResult { LineNumber = 1, Gtin = "G1", ExpectedQty = 10, ReceivedQty = 10 },
                new LineResult { LineNumber = 2, Gtin = "G2", ExpectedQty = 5, ReceivedQty = 3 }
            }
        };
        var note = Mapping.ToDeliveryNote(e, DateTimeOffset.UtcNow);
        Assert.True(note.HasShortages);
        Assert.Equal(2, note.Lines.Single(l => l.LineNumber == 2).ShortQty);
    }
}

// ---- Test doubles ----

internal sealed class FakeD365 : ID365Client
{
    public int Picking, Shipment, Delivery, Case, Adjustment;
    public int FailTimes;   // fail the next N calls (any endpoint)

    private void MaybeFail()
    {
        if (FailTimes > 0) { FailTimes--; throw new InvalidOperationException("F&O transient error"); }
    }
    public Task PostPickingConfirmationAsync(PickingConfirmation c, CancellationToken ct = default) { MaybeFail(); Picking++; return Task.CompletedTask; }
    public Task PostShipmentConfirmationAsync(ShipmentConfirmation c, CancellationToken ct = default) { MaybeFail(); Shipment++; return Task.CompletedTask; }
    public Task PostDeliveryNoteAsync(DeliveryNotePosting c, CancellationToken ct = default) { MaybeFail(); Delivery++; return Task.CompletedTask; }
    public Task CreateShortageCaseAsync(DeliveryNotePosting c, CancellationToken ct = default) { Case++; return Task.CompletedTask; }
    public Task PostQuantityAdjustmentAsync(DeliveryNotePosting c, CancellationToken ct = default) { Adjustment++; return Task.CompletedTask; }
}

internal sealed class FakeDlq : IDeadLetterSink
{
    public int Count;
    public Task DeadLetterAsync(string channel, string eventId, string payloadJson, string error, CancellationToken ct = default)
    { Count++; return Task.CompletedTask; }
}

public class PostingServiceTests
{
    private static D365PostingService New(FakeD365 d365, FakeDlq dlq, out InMemoryIdempotencyStore idem, ShortageHandling sh = ShortageHandling.CreateCase)
    {
        idem = new InMemoryIdempotencyStore();
        return new D365PostingService(d365, idem, dlq,
            new PostingOptions { MaxAttempts = 4, ShortageHandling = sh },
            NullLogger<D365PostingService>.Instance,
            delay: (_, _) => Task.CompletedTask);   // no real sleeping
    }

    private static TrackingOutboundEvent Receiving(string id, decimal exp, decimal recv) => new()
    {
        EventId = id, Kind = TrackingEventKind.ReceivingComplete, OrderNumber = "SO1",
        Lines = new[] { new LineResult { LineNumber = 1, Gtin = "G1", ExpectedQty = exp, ReceivedQty = recv } }
    };

    [Fact]
    public async Task Posts_delivery_note()
    {
        var d365 = new FakeD365(); var dlq = new FakeDlq();
        var svc = New(d365, dlq, out _);
        var r = await svc.PostAsync(Receiving("e1", 5, 5));
        Assert.True(r.Posted);
        Assert.Equal(1, d365.Delivery);
        Assert.Equal(0, d365.Case);          // clean -> no case
    }

    [Fact]
    public async Task Shortage_creates_case_by_default()
    {
        var d365 = new FakeD365(); var dlq = new FakeDlq();
        var svc = New(d365, dlq, out _);
        await svc.PostAsync(Receiving("e1", 5, 3));
        Assert.Equal(1, d365.Delivery);
        Assert.Equal(1, d365.Case);
    }

    [Fact]
    public async Task Shortage_can_post_quantity_adjustment()
    {
        var d365 = new FakeD365(); var dlq = new FakeDlq();
        var svc = New(d365, dlq, out _, ShortageHandling.QuantityAdjustment);
        await svc.PostAsync(Receiving("e1", 5, 3));
        Assert.Equal(1, d365.Adjustment);
        Assert.Equal(0, d365.Case);
    }

    [Fact]
    public async Task Duplicate_event_is_not_posted_twice()
    {
        var d365 = new FakeD365(); var dlq = new FakeDlq();
        var svc = New(d365, dlq, out _);
        await svc.PostAsync(Receiving("dup", 5, 5));
        var second = await svc.PostAsync(Receiving("dup", 5, 5));
        Assert.True(second.Duplicate);
        Assert.Equal(1, d365.Delivery);      // only once
    }

    [Fact]
    public async Task Retries_then_succeeds()
    {
        var d365 = new FakeD365 { FailTimes = 2 };   // fail twice, succeed on 3rd
        var dlq = new FakeDlq();
        var svc = New(d365, dlq, out _);
        var r = await svc.PostAsync(Receiving("e1", 5, 5));
        Assert.True(r.Posted);
        Assert.Equal(1, d365.Delivery);
        Assert.Equal(0, dlq.Count);
    }

    [Fact]
    public async Task Exhausts_retries_then_dead_letters()
    {
        var d365 = new FakeD365 { FailTimes = 99 };  // always fail
        var dlq = new FakeDlq();
        var svc = New(d365, dlq, out var idem);
        var r = await svc.PostAsync(Receiving("e1", 5, 5));
        Assert.True(r.DeadLettered);
        Assert.Equal(1, dlq.Count);
        Assert.False(await idem.SeenAsync("d365-inbound", "e1"));  // not marked processed
    }
}
