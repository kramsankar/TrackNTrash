using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.ReceivingApp.Services;

/// <summary>Talks to the live TrackNTrash tracking API (ASN + receiving endpoints).</summary>
public sealed class TrackApiClient
{
    public const string BaseUrl = "https://app-tracking-tracktrash-dev-4ymqn2.azurewebsites.net";

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    public TrackApiClient() => _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };

    public async Task<bool> HealthAsync()
    {
        try { return (await _http.GetAsync("/health")).IsSuccessStatusCode; } catch { return false; }
    }

    /// <summary>Seed the expected tray contents (ASN) for a store.</summary>
    public async Task UpsertAsnAsync(string trayQr, string storeCode, IEnumerable<string> payloads)
    {
        var cartons = payloads.Select((p, i) => new { payload = p, orderLineId = 1L + i, gtin = (string?)null }).ToArray();
        var resp = await _http.PutAsJsonAsync("/asn", new { trayQr, storeCode, expectedCartons = cartons }, Json);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<StartResp?> StartAsync(string trayQr, string storeCode)
    {
        var resp = await _http.PostAsJsonAsync("/receiving/start", new { trayQr, storeCode }, Json);
        if (!resp.IsSuccessStatusCode) return null;   // 404 = no ASN
        return await resp.Content.ReadFromJsonAsync<StartResp>(Json);
    }

    public async Task<ScanResp?> ScanAsync(string sessionId, string payload)
    {
        var resp = await _http.PostAsJsonAsync($"/receiving/{sessionId}/scan", new { payload }, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ScanResp>(Json);
    }

    public async Task<SummaryResp?> CompleteAsync(string sessionId, string deviceId, string receiverName)
    {
        var resp = await _http.PostAsJsonAsync($"/receiving/{sessionId}/complete",
            new { deviceId, receiverName, signatureBlobUri = "pod/sig/desktop.png" }, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SummaryResp>(Json);
    }
}

public sealed record StartResp
{
    public string SessionId { get; init; } = "";
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public int Expected { get; init; }
}

public sealed record ScanResp
{
    public string Outcome { get; init; } = "";        // Received | Duplicate | Over | Damaged
    public string Payload { get; init; } = "";
    public string? CorrectStoreCode { get; init; }
    public string Message { get; init; } = "";
    public int Received { get; init; }
    public int Expected { get; init; }
    public int Unexpected { get; init; }
}

public sealed record SummaryResp
{
    public string TrayQr { get; init; } = "";
    public int ExpectedCount { get; init; }
    public int ReceivedCount { get; init; }
    public List<string> ShortPayloads { get; init; } = new();
    public List<string> OverPayloads { get; init; } = new();
    public bool Clean { get; init; }
}
