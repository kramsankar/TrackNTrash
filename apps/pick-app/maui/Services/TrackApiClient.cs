using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.PickApp.Services;

/// <summary>Talks to the live TrackNTrash tracking API.</summary>
public sealed class TrackApiClient
{
    // Live Azure deployment. Override with an env/config for local runs.
    public const string BaseUrl = "https://app-tracking-tracktrash-dev-z3yo3x.azurewebsites.net";

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    /// <summary>The signed-in user's token; every guarded endpoint needs it.</summary>
    public AuthSession Auth { get; } = new();

    /// <summary>Exposed so the sign-in card can post credentials on the same connection.</summary>
    public HttpClient Http => _http;

    public TrackApiClient()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        // A token persisted from a previous run keeps the scanner usable without re-typing.
        Auth.Apply(_http);
    }

    public async Task<bool> HealthAsync()
    {
        try { var r = await _http.GetAsync("/health"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    /// <summary>Create an order (master data) and return its order-line ids.</summary>
    public async Task<long[]> CreateOrderAsync(string orderNumber, string storeCode, string gtin, int expectedCartons)
    {
        var body = new
        {
            orderNumber, storeCode, erpReference = "PICKAPP-" + orderNumber,
            lines = new[] { new { lineNumber = 1, gtin, orderedQty = expectedCartons * 24, uom = "EA", expectedCartonCount = expectedCartons } }
        };
        var resp = await _http.PostAsJsonAsync("/orders", body, Json);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<OrderResp>(Json);
        return doc?.OrderLineIds ?? Array.Empty<long>();
    }

    /// <summary>Post the tray-build-complete scan for a line (advances Ordered → Picked).</summary>
    public async Task<ScanResp?> TrayBuildCompleteAsync(long orderLineId, string trayQr, string deviceId, string user, string cartonsJson)
    {
        var body = new
        {
            clientEventId = $"{deviceId}:{orderLineId}:{Guid.NewGuid():N}",
            deviceId, userId = user, eventType = "TrayBuildComplete", checkpoint = "PickTrayBuild",
            orderLineId, trayQr, meta = cartonsJson
        };
        var resp = await _http.PostAsJsonAsync("/events/scan", body, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ScanResp>(Json);
    }

    public async Task<LineState?> GetLineStateAsync(long orderLineId)
        => await _http.GetFromJsonAsync<LineState>($"/shipment-lines/{orderLineId}/state", Json);

    /// <summary>
    /// Registers a carton so its individual units can be counted. Returns the carton id
    /// that <see cref="CountItemsAsync"/> needs — item counts hang off the carton, not the QR.
    /// </summary>
    public async Task<long> CreateCartonAsync(long orderLineId, string gtin, string serial,
        int expectedItemCount, string itemIdentification)
    {
        var body = new { orderLineId, gtin, serial, expectedItemCount, itemIdentification };
        var resp = await _http.PostAsJsonAsync("/cartons", body, Json);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<CartonResp>(Json);
        return doc?.CartonId ?? 0;
    }

    /// <summary>
    /// Records how many units are actually in a carton. Barcoded units come through
    /// scannedBarcodes; unlabelled ones are counted by camera and come through visionCount.
    /// The API decides the verdict (OK / SHORT / OVER) and raises the exception itself.
    /// </summary>
    public async Task<ItemCountResp?> CountItemsAsync(long cartonId, IEnumerable<string> scannedBarcodes,
        int? visionCount, string deviceId, string checkpoint = "PickTrayBuild")
    {
        var body = new
        {
            cartonId, checkpoint, deviceId,
            scannedBarcodes = scannedBarcodes.ToList(),
            visionCount
        };
        var resp = await _http.PostAsJsonAsync("/items/count", body, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ItemCountResp>(Json);
    }

    private sealed record CartonResp([property: JsonPropertyName("cartonId")] long CartonId);

    private sealed record OrderResp([property: JsonPropertyName("orderLineIds")] long[] OrderLineIds);
}

public sealed record ScanResp
{
    public bool Accepted { get; init; }
    public bool Duplicate { get; init; }
    public long? ScanEventId { get; init; }
    public string? NewState { get; init; }
    public bool TransitionLegal { get; init; }
}

public sealed record LineState
{
    public long OrderLineId { get; init; }
    public string CurrentState { get; init; } = "";
    public long? LastEventId { get; init; }
}

/// <summary>Mirrors the API's CountResult. Verdict is OK, SHORT or OVER.</summary>
public sealed record ItemCountResp
{
    public long ItemCountId { get; init; }
    public int Expected { get; init; }
    public int Scanned { get; init; }
    public int? Vision { get; init; }
    public string Verdict { get; init; } = "";
    public string Detail { get; init; } = "";
}
