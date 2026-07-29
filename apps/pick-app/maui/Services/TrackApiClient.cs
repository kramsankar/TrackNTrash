using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.PickApp.Services;

/// <summary>Talks to the live TrackNTrash tracking API.</summary>
public sealed class TrackApiClient
{
    // Live Azure deployment. Override with an env/config for local runs.
    public const string BaseUrl = "https://app-tracking-tracktrash-dev-4ymqn2.azurewebsites.net";

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
