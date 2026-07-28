using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.ReceivingApp;

/// <summary>HTTP client over the Module 8 receiving endpoints. Shares the app family with the driver app.</summary>
public sealed class ReceivingApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    public ReceivingApiClient(HttpClient http) => _http = http;

    public async Task<StartReceivingResponse?> StartAsync(string trayQr, string storeCode, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/receiving/start", new { trayQr, storeCode }, Json, ct);
        if (!resp.IsSuccessStatusCode) return null;   // 404 = no ASN
        return await resp.Content.ReadFromJsonAsync<StartReceivingResponse>(Json, ct);
    }

    public async Task<CartonScanResult?> ScanAsync(string sessionId, string payload, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/receiving/{sessionId}/scan", new { payload }, Json, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CartonScanResult>(Json, ct);
    }

    public async Task<CartonScanResult?> FlagDamagedAsync(string sessionId, string payload, string photoBlobUri, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/receiving/{sessionId}/damaged", new { payload, photoBlobUri }, Json, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CartonScanResult>(Json, ct);
    }

    public async Task<ReceivingSummary?> CompleteAsync(string sessionId, string deviceId, string receiverName, string? signatureBlobUri, string? deliveryPhotoBlobUri, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/receiving/{sessionId}/complete",
            new { deviceId, receiverName, signatureBlobUri, deliveryPhotoBlobUri }, Json, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ReceivingSummary>(Json, ct);
    }

    public async Task ReturnTrayAsync(string trayQr, string vehicleReg, string deviceId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/receiving/return-tray", new { trayQr, vehicleReg, deviceId }, Json, ct);
        resp.EnsureSuccessStatusCode();
    }
}

public sealed record StartReceivingResponse
{
    public string SessionId { get; init; } = "";
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public int Expected { get; init; }
    public List<ExpectedCartonDto> ExpectedCartons { get; init; } = new();
}

public sealed record ExpectedCartonDto
{
    public string Payload { get; init; } = "";
    public long OrderLineId { get; init; }
    public string? Gtin { get; init; }
}

public sealed record CartonScanResult
{
    public string Outcome { get; init; } = "";        // Received | Duplicate | Over | Damaged
    public string Payload { get; init; } = "";
    public string? CorrectStoreCode { get; init; }
    public string Message { get; init; } = "";
    public int Received { get; init; }
    public int Expected { get; init; }
    public int Unexpected { get; init; }
}

public sealed record ReceivingSummary
{
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public int ExpectedCount { get; init; }
    public int ReceivedCount { get; init; }
    public List<string> ShortPayloads { get; init; } = new();
    public List<string> OverPayloads { get; init; } = new();
    public List<string> DamagedPayloads { get; init; } = new();
    public bool Clean { get; init; }
}
