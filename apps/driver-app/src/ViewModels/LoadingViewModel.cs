using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrackNTrash.DriverApp.Services;

namespace TrackNTrash.DriverApp.ViewModels;

/// <summary>
/// Drives the loading screen: opens a trip, processes tray scans, surfaces wrong-trip
/// rejections, and reflects the lock/depart lifecycle. Scans are debounced by the view.
/// </summary>
public sealed class LoadingViewModel : INotifyPropertyChanged
{
    private readonly TrackingApiClient _api;
    private readonly string _deviceId;
    private readonly string? _userId;

    public LoadingViewModel(TrackingApiClient api, string deviceId, string? userId)
    {
        _api = api;
        _deviceId = deviceId;
        _userId = userId;
    }

    public ObservableCollection<TripLoadDto> Trays { get; } = new();

    private TripDto? _trip;
    public string TripNumber => _trip?.TripNumber ?? "—";
    public string Progress => _trip is null ? "" : $"{Trays.Count(t => t.Loaded)} / {Trays.Count} loaded";
    public bool IsLocked => string.Equals(_trip?.Status, "Loaded", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(_trip?.Status, "Departed", StringComparison.OrdinalIgnoreCase);

    private string _banner = "";
    public string Banner { get => _banner; private set { _banner = value; OnPropertyChanged(); } }

    private bool _bannerIsError;
    public bool BannerIsError { get => _bannerIsError; private set { _bannerIsError = value; OnPropertyChanged(); } }

    public async Task OpenTripAsync(string manifestOrTripQr)
    {
        // Manifest QR maps to a trip number server-side; pass through.
        var tripNumber = manifestOrTripQr.StartsWith("MANIFEST-", StringComparison.OrdinalIgnoreCase)
            ? manifestOrTripQr["MANIFEST-".Length..]
            : manifestOrTripQr;

        _trip = await _api.GetTripAsync(tripNumber);
        Trays.Clear();
        if (_trip is not null)
            foreach (var load in _trip.Loads.OrderBy(l => l.Planned.StopSequence))
                Trays.Add(load);
        SetBanner(_trip is null ? "Trip not found" : $"Trip {_trip.TripNumber} opened", _trip is null);
        RaiseHeader();
    }

    public async Task ScanTrayAsync(string trayQr)
    {
        if (_trip is null) { SetBanner("Open a trip first", true); return; }
        if (IsLocked) { SetBanner("Trip is locked", true); return; }

        var result = await _api.LoadTrayAsync(_trip.TripNumber, trayQr, _deviceId, _userId);
        if (result is null) { SetBanner("No response — will retry", true); return; }

        switch (result.Outcome)
        {
            case "Loaded":
                MarkLoaded(trayQr);
                if (result.TripNowLocked && _trip is not null) _trip = _trip with { Status = "Loaded" };
                SetBanner(result.TripNowLocked ? "✅ Trip complete & locked" : $"✅ Loaded {trayQr}", false);
                break;
            case "WrongTrip":
                // Full red alert with the correct trip number.
                SetBanner(result.CorrectTripNumber is not null
                    ? $"⛔ WRONG TRIP — load on {result.CorrectTripNumber}"
                    : "⛔ WRONG TRIP — not planned on any trip", true);
                break;
            case "AlreadyLoaded":
                SetBanner($"Already loaded {trayQr}", false);
                break;
            case "TripLocked":
                SetBanner("Trip locked — loading closed", true);
                break;
        }
        RaiseHeader();
    }

    public async Task DepartAsync()
    {
        if (_trip is null) return;
        var ok = await _api.DepartAsync(_trip.TripNumber, _deviceId);
        SetBanner(ok ? "🚚 Departed — in transit" : "Cannot depart yet", !ok);
    }

    private void MarkLoaded(string trayQr)
    {
        for (int i = 0; i < Trays.Count; i++)
            if (string.Equals(Trays[i].Planned.TrayQr, trayQr, StringComparison.OrdinalIgnoreCase))
                Trays[i] = Trays[i] with { Loaded = true };
    }

    private void SetBanner(string text, bool isError) { Banner = text; BannerIsError = isError; }
    private void RaiseHeader() { OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(IsLocked)); OnPropertyChanged(nameof(TripNumber)); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
