using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrackNTrash.ReceivingApp;

/// <summary>
/// Drives the receiving screen: open a tray's ASN, reconcile carton scans with a live tally,
/// surface OVER (with correct store) and damage, then capture POD and complete.
/// </summary>
public sealed class ReceivingViewModel : INotifyPropertyChanged
{
    private readonly ReceivingApiClient _api;
    private readonly string _deviceId;
    private string? _sessionId;

    public ReceivingViewModel(ReceivingApiClient api, string deviceId)
    {
        _api = api;
        _deviceId = deviceId;
    }

    public ObservableCollection<ExpectedCartonDto> Expected { get; } = new();

    public string TrayQr { get; private set; } = "—";
    public string StoreCode { get; private set; } = "";

    private int _received, _expected, _over;
    public string Tally => $"{_received} / {_expected} received · {_over} over";

    private string _banner = "";
    public string Banner { get => _banner; private set { _banner = value; OnPropertyChanged(); } }
    private bool _bannerIsError;
    public bool BannerIsError { get => _bannerIsError; private set { _bannerIsError = value; OnPropertyChanged(); } }

    public bool CanComplete => _sessionId is not null;

    public async Task OpenTrayAsync(string trayQr, string storeCode)
    {
        var start = await _api.StartAsync(trayQr, storeCode);
        if (start is null) { SetBanner($"No ASN for {trayQr} @ {storeCode}", true); return; }

        _sessionId = start.SessionId;
        TrayQr = start.TrayQr;
        StoreCode = start.StoreCode;
        _expected = start.Expected;
        _received = 0; _over = 0;
        Expected.Clear();
        foreach (var c in start.ExpectedCartons) Expected.Add(c);
        SetBanner($"Receiving {start.Expected} cartons for {storeCode}", false);
        RaiseHeader();
    }

    public async Task ScanAsync(string payload)
    {
        if (_sessionId is null) { SetBanner("Scan the tray first", true); return; }
        var r = await _api.ScanAsync(_sessionId, payload);
        if (r is null) return;

        _received = r.Received; _over = r.Unexpected;
        switch (r.Outcome)
        {
            case "Received":  SetBanner($"✅ {payload}", false); break;
            case "Duplicate": SetBanner($"Already received {payload}", false); break;
            case "Over":      SetBanner(r.CorrectStoreCode is not null
                                    ? $"⛔ OVER — belongs to {r.CorrectStoreCode}"
                                    : "⛔ OVER — unknown carton", true); break;
        }
        RaiseHeader();
    }

    /// <summary>Flag damage. The caller must have captured a photo (blob uri) first (min 1).</summary>
    public async Task FlagDamagedAsync(string payload, string photoBlobUri)
    {
        if (_sessionId is null) return;
        if (string.IsNullOrWhiteSpace(photoBlobUri)) { SetBanner("Photo required for damage", true); return; }
        await _api.FlagDamagedAsync(_sessionId, payload, photoBlobUri);
        SetBanner($"📷 Damage recorded: {payload}", true);
    }

    public async Task<ReceivingSummary?> CompleteAsync(string receiverName, string? signatureBlobUri, string? deliveryPhotoBlobUri)
    {
        if (_sessionId is null) return null;
        if (string.IsNullOrWhiteSpace(receiverName)) { SetBanner("Receiver name required", true); return null; }
        var summary = await _api.CompleteAsync(_sessionId, _deviceId, receiverName, signatureBlobUri, deliveryPhotoBlobUri);
        _sessionId = null;
        SetBanner(summary?.Clean == true ? "✅ Clean delivery" : "⚠️ Delivery with discrepancies", summary?.Clean != true);
        OnPropertyChanged(nameof(CanComplete));
        return summary;
    }

    private void SetBanner(string t, bool e) { Banner = t; BannerIsError = e; }
    private void RaiseHeader()
    {
        OnPropertyChanged(nameof(Tally)); OnPropertyChanged(nameof(TrayQr));
        OnPropertyChanged(nameof(StoreCode)); OnPropertyChanged(nameof(CanComplete));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
