using Microsoft.Extensions.Logging.Abstractions;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using TrackNTrash.Tracking.Core.Trips;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class TripServiceTests
{
    private static (TripService trips, IngestionService ingest, InMemoryShipmentStateStore states) NewStack()
    {
        var events = new InMemoryEventStore();
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var manifests = new InMemoryManifestStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);
        var ingest = new IngestionService(events, states, exc, manifests, notifier,
            new ShipmentStateMachine(), new ExceptionSeverityMatrix(),
            Array.Empty<IIngestExceptionRule>(), NullLogger<IngestionService>.Instance);
        var trips = new TripService(new InMemoryTripStore(), ingest, new ExceptionSeverityMatrix(),
            NullLogger<TripService>.Instance);
        return (trips, ingest, states);
    }

    private static async Task StageLine(IngestionService ingest, long orderLineId)
    {
        // Ordered -> Picked -> Staged
        await ingest.IngestAsync(new ScanEventInput { ClientEventId = $"{orderLineId}-b", DeviceId = "d", EventType = "TrayBuildComplete", OrderLineId = orderLineId });
        await ingest.IngestAsync(new ScanEventInput { ClientEventId = $"{orderLineId}-d", DeviceId = "d", EventType = "DockVerification", Verdict = "PASS", OrderLineId = orderLineId });
    }

    [Fact]
    public async Task Create_trip_assigns_number_and_manifest_qr()
    {
        var (trips, _, _) = NewStack();
        var trip = await trips.CreateAsync(new CreateTripRequest
        {
            VehicleReg = "AB12 CDE",
            Stops = new[] { new TripStopDef { Sequence = 1, StoreCode = "S1" } },
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-LDN1-000001", StopSequence = 1 } }
        });

        Assert.StartsWith("TRIP-", trip.TripNumber);
        Assert.StartsWith("MANIFEST-TRIP-", trip.ManifestQr);
        Assert.Equal(TripStatus.Planned, trip.Status);
    }

    [Fact]
    public async Task Loading_all_trays_locks_trip_and_sets_lines_Loaded()
    {
        var (trips, ingest, states) = NewStack();
        await StageLine(ingest, 1001);
        var trip = await trips.CreateAsync(new CreateTripRequest
        {
            VehicleReg = "AB12 CDE",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-LDN1-000001", StopSequence = 1, OrderLineIds = new long[] { 1001 } } }
        });

        var r = await trips.LoadTrayScanAsync(trip.TripNumber, "TRAY-LDN1-000001", "driver-dev", "drv@co");

        Assert.Equal(LoadScanOutcome.Loaded, r.Outcome);
        Assert.True(r.TripNowLocked);
        Assert.Equal(ShipmentState.Loaded, (await states.GetOrCreateAsync(1001)).CurrentState);
    }

    [Fact]
    public async Task Wrong_trip_scan_is_rejected_with_correct_trip_number_and_exception()
    {
        var (trips, ingest, states) = NewStack();
        await StageLine(ingest, 2001);

        var tripA = await trips.CreateAsync(new CreateTripRequest { VehicleReg = "A",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-A", OrderLineIds = new long[] { 2001 } } } });
        var tripB = await trips.CreateAsync(new CreateTripRequest { VehicleReg = "B",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-B" } } });

        // Scan tray A onto trip B -> wrong trip
        var r = await trips.LoadTrayScanAsync(tripB.TripNumber, "TRAY-A", "driver-dev", "drv@co");

        Assert.Equal(LoadScanOutcome.WrongTrip, r.Outcome);
        Assert.Equal(tripA.TripNumber, r.CorrectTripNumber);
        Assert.NotNull(r.Exception);
        Assert.Equal(ExceptionType.WrongTrip, r.Exception!.Type);
        // Line stayed Staged (not loaded onto the wrong trip)
        Assert.Equal(ShipmentState.Staged, (await states.GetOrCreateAsync(2001)).CurrentState);
    }

    [Fact]
    public async Task Unknown_tray_is_wrong_trip_with_no_correct_trip()
    {
        var (trips, _, _) = NewStack();
        var trip = await trips.CreateAsync(new CreateTripRequest { VehicleReg = "A",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-A" } } });

        var r = await trips.LoadTrayScanAsync(trip.TripNumber, "TRAY-UNKNOWN", "dev", null);
        Assert.Equal(LoadScanOutcome.WrongTrip, r.Outcome);
        Assert.Null(r.CorrectTripNumber);
    }

    [Fact]
    public async Task Depart_transitions_loaded_lines_to_InTransit()
    {
        var (trips, ingest, states) = NewStack();
        await StageLine(ingest, 3001);
        var trip = await trips.CreateAsync(new CreateTripRequest { VehicleReg = "A",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-A", OrderLineIds = new long[] { 3001 } } } });
        await trips.LoadTrayScanAsync(trip.TripNumber, "TRAY-A", "dev", null);

        var ok = await trips.DepartAsync(trip.TripNumber, "telematics");

        Assert.True(ok);
        Assert.Equal(ShipmentState.InTransit, (await states.GetOrCreateAsync(3001)).CurrentState);
    }

    [Fact]
    public async Task Loading_a_locked_trip_is_blocked()
    {
        var (trips, ingest, _) = NewStack();
        await StageLine(ingest, 4001);
        var trip = await trips.CreateAsync(new CreateTripRequest { VehicleReg = "A",
            PlannedTrays = new[] { new PlannedTray { TrayQr = "TRAY-A", OrderLineIds = new long[] { 4001 } } } });
        await trips.LoadTrayScanAsync(trip.TripNumber, "TRAY-A", "dev", null); // locks (all loaded)

        var r = await trips.LoadTrayScanAsync(trip.TripNumber, "TRAY-A", "dev", null);
        Assert.Equal(LoadScanOutcome.TripLocked, r.Outcome);
    }
}
