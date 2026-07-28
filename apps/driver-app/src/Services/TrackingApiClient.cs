using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.DriverApp.Services;

/// <summary>
/// Thin HTTP client over the Module 6/7 tracking API. Shared by the driver (M7) and
/// receiving (M8) apps. All writes carry a client event id so retries are idempotent.
/// </summary>
public sealed class TrackingApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public TrackingApiClient(HttpClient http) => _http = http;

    public async Task<TripDto?> GetTripAsync(string tripNumber, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<TripDto>($"/trips/{tripNumber}", Json, ct);

    public async Task<LoadScanResultDto?> LoadTrayAsync(string tripNumber, string trayQr, string deviceId, string? userId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/trips/{tripNumber}/load",
            new { trayQr, deviceId, userId }, Json, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LoadScanResultDto>(Json, ct);
    }

    public async Task<bool> DepartAsync(string tripNumber, string deviceId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/events/telemetry",
            new { tripNumber, @event = "depart", deviceId }, Json, ct);
        return resp.IsSuccessStatusCode;
    }
}

// ---- DTOs mirrored from the API ----

public sealed record TripDto
{
    public string TripNumber { get; init; } = "";
    public string ManifestQr { get; init; } = "";
    public string VehicleReg { get; init; } = "";
    public string? DriverName { get; init; }
    public string Status { get; init; } = "";
    public List<TripStopDto> Stops { get; init; } = new();
    public List<TripLoadDto> Loads { get; init; } = new();
}

public sealed record TripStopDto
{
    public int Sequence { get; init; }
    public string StoreCode { get; init; } = "";
}

public sealed record TripLoadDto
{
    public PlannedTrayDto Planned { get; init; } = new();
    public bool Loaded { get; init; }
}

public sealed record PlannedTrayDto
{
    public string TrayQr { get; init; } = "";
    public int StopSequence { get; init; }
}

public sealed record LoadScanResultDto
{
    public string Outcome { get; init; } = "";           // Loaded | AlreadyLoaded | WrongTrip | TripLocked
    public string TrayQr { get; init; } = "";
    public string TripNumber { get; init; } = "";
    public string? CorrectTripNumber { get; init; }
    public bool TripNowLocked { get; init; }
    public string Message { get; init; } = "";
}
