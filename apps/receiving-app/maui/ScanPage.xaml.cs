using ZXing.Net.Maui;

namespace TrackNTrash.ReceivingApp;

/// <summary>Modal camera QR scanner. Returns the decoded value (or null if cancelled).</summary>
public partial class ScanPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    public Task<string?> Result => _tcs.Task;

    public ScanPage()
    {
        InitializeComponent();
        reader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    private void OnDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(value)) return;
        reader.IsDetecting = false;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _tcs.TrySetResult(value);
            await Navigation.PopModalAsync();
        });
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }
}
