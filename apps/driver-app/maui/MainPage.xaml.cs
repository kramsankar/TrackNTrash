using TrackNTrash.DriverApp.Services;

namespace TrackNTrash.DriverApp;

public partial class MainPage : ContentPage
{
    private readonly TrackApiClient _api = new();
    private string? _tripNumber;
    private const string DeviceId = "DRIVER-DESKTOP";

    public MainPage()
    {
        InitializeComponent();
        _ = CheckHealth();
        RefreshAuthUi();
    }

    private async Task CheckHealth()
    {
        bool ok = await _api.HealthAsync();
        lblConn.Text = ok ? $"● Connected · {TrackApiClient.BaseUrl}" : "○ Offline — cannot reach the API";
        lblConn.TextColor = ok ? Color.FromArgb("#43b477") : Color.FromArgb("#f2675e");
    }

    private async void OnCreateTrip(object? sender, EventArgs e)
    {
        if (!long.TryParse(txtLine.Text, out var lineId)) { Banner("Enter a valid order line id", true); return; }
        btnCreate.IsEnabled = false;
        try
        {
            var trip = await _api.CreateTripAsync(txtVehicle.Text.Trim(), txtRoute.Text.Trim(), txtStore.Text.Trim(), txtTray.Text.Trim(), lineId);
            _tripNumber = trip?.TripNumber;
            lblTrip.Text = $"Trip {_tripNumber} · manifest {trip?.ManifestQr} · {trip?.Trays} tray(s)";
            txtScan.Text = txtTray.Text;
            Banner($"Trip {_tripNumber} created — scan trays to load", false);
            Log($"✅ {_tripNumber} created");
        }
        catch (Exception ex) { Banner("Failed: " + ex.Message, true); Log("❌ " + ex.Message); }
        finally { btnCreate.IsEnabled = true; }
    }

    // Camera scan (phone targets): scan a tray QR and load it.
    private async void OnScanCamera(object? sender, EventArgs e)
    {
        var page = new ScanPage();
        await Navigation.PushModalAsync(page);
        var code = await page.Result;
        if (!string.IsNullOrWhiteSpace(code)) { txtScan.Text = code.Trim(); OnLoadScan(this, EventArgs.Empty); }
    }

    private async void OnLoadScan(object? sender, EventArgs e)
    {
        var trayQr = txtScan.Text?.Trim() ?? "";
        if (trayQr.Length == 0) return;
        if (_tripNumber is null) { Banner("Create a trip first", true); return; }
        try
        {
            var r = await _api.LoadTrayAsync(_tripNumber, trayQr, DeviceId);
            switch (r?.Outcome)
            {
                case "Loaded":
                    Banner(r.TripNowLocked ? "✅ Loaded — trip complete & locked" : $"✅ Loaded {trayQr}", false);
                    btnDepart.IsEnabled = true; break;
                case "WrongTrip":
                    Banner(r.CorrectTripNumber is not null ? $"⛔ WRONG TRIP\nLoad on {r.CorrectTripNumber}" : "⛔ WRONG TRIP — not planned", true);
                    break;
                case "AlreadyLoaded": Banner($"Already loaded {trayQr}", false); break;
                case "TripLocked": Banner("Trip locked — loading closed", true); break;
                default: Banner(r?.Message ?? "No response", true); break;
            }
            Log($"load {trayQr} → {r?.Outcome}");
        }
        catch (Exception ex) { Banner("Load failed: " + ex.Message, true); Log("❌ " + ex.Message); }
    }

    private async void OnDepart(object? sender, EventArgs e)
    {
        if (_tripNumber is null) return;
        var ok = await _api.DepartAsync(_tripNumber, DeviceId);
        Banner(ok ? "🚚 Departed — in transit" : "Cannot depart yet", !ok);
        Log($"depart {_tripNumber} → {(ok ? "InTransit" : "failed")}");
    }

    private void Banner(string text, bool isError)
    {
        banner.IsVisible = true;
        banner.BackgroundColor = Color.FromArgb(isError ? "#c0261e" : "#1f8a4d");
        lblBanner.Text = text;
    }

    private void Log(string line) => lblLog.Text = line + "\n" + lblLog.Text;

    // ---- Sign in -------------------------------------------------------------------
    // The tracking API guards every operational endpoint, so nothing below works until
    // a token is in hand. A token from a previous run is restored on construction.

    private void RefreshAuthUi()
    {
        bool on = _api.Auth.IsSignedIn;
        workArea.IsEnabled = on;
        workArea.Opacity = on ? 1 : 0.45;
        btnSignIn.IsVisible = !on;
        btnSignOut.IsVisible = on;
        txtUser.IsVisible = !on;
        txtPass.IsVisible = !on;
        lblAuth.Text = on
            ? $"✓ Signed in as {_api.Auth.Username}"
            : "The tracking API only accepts scans from a signed-in user.";
        lblAuth.TextColor = Color.FromArgb(on ? "#43b477" : "#9aa6ba");
    }

    private async void OnSignIn(object? sender, EventArgs e)
    {
        btnSignIn.IsEnabled = false;
        try
        {
            var error = await _api.Auth.SignInAsync(_api.Http, txtUser.Text?.Trim() ?? "", txtPass.Text ?? "");
            if (error is not null) { Banner(error, true); return; }
            txtPass.Text = "";
            RefreshAuthUi();
            Banner($"Signed in as {_api.Auth.Username}", false);
        }
        finally { btnSignIn.IsEnabled = true; }
    }

    private void OnSignOut(object? sender, EventArgs e)
    {
        _api.Auth.SignOut(_api.Http);
        RefreshAuthUi();
        Banner("Signed out.", false);
    }
}
