using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackNTrash.DriverApp.Services;

/// <summary>Talks to the live TrackNTrash tracking API (trip + loading endpoints).</summary>
public sealed class TrackApiClient
{
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
        try { return (await _http.GetAsync("/health")).IsSuccessStatusCode; } catch { return false; }
    }

    /// <summary>Create a trip with one planned tray carrying the given order line.</summary>
    public async Task<TripResp?> CreateTripAsync(string vehicleReg, string routeCode, string storeCode, string trayQr, long orderLineId)
    {
        var body = new
        {
            vehicleReg, routeCode,
            stops = new[] { new { sequence = 1, storeCode } },
            plannedTrays = new[] { new { trayQr, stopSequence = 1, orderLineIds = new[] { orderLineId } } }
        };
        var resp = await _http.PostAsJsonAsync("/trips", body, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TripResp>(Json);
    }

    /// <summary>Scan a tray onto a trip at the loading dock.</summary>
    public async Task<LoadResp?> LoadTrayAsync(string tripNumber, string trayQr, string deviceId)
    {
        var resp = await _http.PostAsJsonAsync($"/trips/{tripNumber}/load", new { trayQr, deviceId }, Json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LoadResp>(Json);
    }

    /// <summary>
    /// Hands an empty tray back to the vehicle on the return leg. Without this the tray
    /// stays recorded at the store and the fleet looks smaller than it is.
    /// </summary>
    public async Task<bool> ReturnEmptyTrayAsync(string trayQr, string vehicleReg, string deviceId)
    {
        var resp = await _http.PostAsJsonAsync("/receiving/return-tray",
            new { trayQr, vehicleReg, deviceId }, Json);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DepartAsync(string tripNumber, string deviceId)
    {
        var resp = await _http.PostAsJsonAsync("/events/telemetry", new { tripNumber, @event = "depart", deviceId }, Json);
        return resp.IsSuccessStatusCode;
    }
}

public sealed record TripResp
{
    public string TripNumber { get; init; } = "";
    public string ManifestQr { get; init; } = "";
    public string Status { get; init; } = "";
    public int Trays { get; init; }
    public int Stops { get; init; }
}

public sealed record LoadResp
{
    public string Outcome { get; init; } = "";        // Loaded | AlreadyLoaded | WrongTrip | TripLocked
    public string TrayQr { get; init; } = "";
    public string TripNumber { get; init; } = "";
    public string? CorrectTripNumber { get; init; }
    public bool TripNowLocked { get; init; }
    public string Message { get; init; } = "";
}
