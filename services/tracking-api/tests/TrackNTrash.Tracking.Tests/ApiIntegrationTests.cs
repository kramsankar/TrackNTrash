using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using TrackNTrash.Tracking.Core;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Match the API's string-enum serialization.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Scan_endpoint_requires_ids()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/events/scan", new { eventType = "TrayBuildComplete" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Scan_then_read_state_reflects_transition()
    {
        var client = _factory.CreateClient();

        var ingest = await client.PostAsJsonAsync("/events/scan", new
        {
            clientEventId = "it-1", deviceId = "dev-it", eventType = "TrayBuildComplete", orderLineId = 9001
        });
        ingest.EnsureSuccessStatusCode();
        var result = await ingest.Content.ReadFromJsonAsync<IngestResult>(Json);
        Assert.Equal(ShipmentState.Picked, result!.NewState);

        var state = await client.GetFromJsonAsync<ShipmentLineStateRecord>("/shipment-lines/9001/state", Json);
        Assert.Equal(ShipmentState.Picked, state!.CurrentState);
    }

    [Fact]
    public async Task Manifest_upsert_then_delta_sync()
    {
        var client = _factory.CreateClient();
        var put = await client.PutAsJsonAsync("/manifests", new
        {
            trayQr = "TRAY-LDN1-000009", expectedCartonCount = 4, expectedCartonPayloads = new[] { "0109..." }
        });
        put.EnsureSuccessStatusCode();

        var resp = await client.GetAsync("/manifests?since=2000-01-01T00:00:00Z");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("TRAY-LDN1-000009", body);
    }
}
