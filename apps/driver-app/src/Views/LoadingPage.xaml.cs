using ZXing.Net.Maui;
using TrackNTrash.DriverApp.ViewModels;

namespace TrackNTrash.DriverApp.Views;

public partial class LoadingPage : ContentPage
{
    private readonly LoadingViewModel _vm;
    private DateTime _lastScan = DateTime.MinValue;
    private string? _lastValue;

    public LoadingPage(LoadingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        Scanner.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,   // QR
            AutoRotate = true,
            Multiple = false
        };
    }

    // Debounce: ZXing fires continuously; ignore repeats of the same value within 1.5s.
    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(value)) return;

        var now = DateTime.UtcNow;
        if (value == _lastValue && (now - _lastScan).TotalSeconds < 1.5) return;
        _lastValue = value;
        _lastScan = now;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // A manifest/trip QR opens the trip; anything else is treated as a tray scan.
            if (value.StartsWith("MANIFEST-", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("TRIP-", StringComparison.OrdinalIgnoreCase))
                await _vm.OpenTripAsync(value);
            else
                await _vm.ScanTrayAsync(value);
        });
    }

    private async void OnDepartClicked(object? sender, EventArgs e) => await _vm.DepartAsync();
}
