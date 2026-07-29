using System.Collections.ObjectModel;
using System.Text.Json;
using TrackNTrash.PickApp.Services;

namespace TrackNTrash.PickApp;

public partial class MainPage : ContentPage
{
    private readonly TrackApiClient _api = new();
    private readonly ObservableCollection<string> _cartons = new();
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private long? _orderLineId;
    private int _expected;
    private const string DeviceId = "PICK-DESKTOP";
    private const string User = "picker@a-squaretechnologies.com";

    public MainPage()
    {
        InitializeComponent();
        listCartons.ItemsSource = _cartons;
        _ = CheckHealth();
        RefreshAuthUi();
    }

    private async Task CheckHealth()
    {
        bool ok = await _api.HealthAsync();
        lblConn.Text = ok ? $"● Connected · {TrackApiClient.BaseUrl}" : "○ Offline — cannot reach the API";
        lblConn.TextColor = ok ? Color.FromArgb("#43b477") : Color.FromArgb("#f2675e");
    }

    private async void OnStartOrder(object? sender, EventArgs e)
    {
        if (!int.TryParse(txtExpected.Text, out _expected) || _expected < 1) { Banner("Enter a valid expected-carton count", true); return; }
        btnStart.IsEnabled = false;
        try
        {
            var ids = await _api.CreateOrderAsync(txtOrder.Text.Trim(), txtStore.Text.Trim(), txtGtin.Text.Trim(), _expected);
            _orderLineId = ids.Length > 0 ? ids[0] : null;
            lblOrder.Text = _orderLineId is null ? "No line created" : $"Order line id {_orderLineId} · expected {_expected} cartons";
            Log($"✅ Order {txtOrder.Text} created (line {_orderLineId})");
            Banner($"Order ready — scan {_expected} cartons into the tray", false);
        }
        catch (Exception ex) { Banner("Failed: " + ex.Message, true); Log("❌ " + ex.Message); }
        finally { btnStart.IsEnabled = true; }
    }

    private void OnCartonScanned(object? sender, EventArgs e)
    {
        var code = txtCarton.Text?.Trim() ?? "";
        txtCarton.Text = "";
        txtCarton.Focus();
        ProcessCarton(code);
    }

    // Camera scan (phone targets): open the ZXing scanner and feed the result to the same logic.
    private async void OnScanCamera(object? sender, EventArgs e)
    {
        var page = new ScanPage();
        await Navigation.PushModalAsync(page);
        var code = await page.Result;
        if (!string.IsNullOrWhiteSpace(code)) ProcessCarton(code.Trim());
    }

    private void ProcessCarton(string code)
    {
        if (code.Length == 0) return;
        if (_orderLineId is null) { Banner("Start an order first", true); return; }
        if (!_seen.Add(code)) { Banner($"Already scanned: {code}", true); return; }
        if (_cartons.Count >= _expected) { Banner("Tray already full for this line", true); return; }

        _cartons.Add(code);
        lblTally.Text = $"{_cartons.Count} / {_expected} scanned";
        Banner($"✅ {code}", false);
        btnComplete.IsEnabled = _cartons.Count > 0;
    }

    private async void OnComplete(object? sender, EventArgs e)
    {
        if (_orderLineId is null) return;
        btnComplete.IsEnabled = false;
        try
        {
            var cartonsJson = JsonSerializer.Serialize(_cartons);
            var r = await _api.TrayBuildCompleteAsync(_orderLineId.Value, txtTray.Text.Trim(), DeviceId, User, cartonsJson);
            var state = await _api.GetLineStateAsync(_orderLineId.Value);
            Banner($"🏁 Tray complete — line is now {state?.CurrentState ?? r?.NewState ?? "?"}", false);
            Log($"➡️ TrayBuildComplete: {_cartons.Count} cartons on {txtTray.Text} → state {state?.CurrentState}");
        }
        catch (Exception ex) { Banner("Complete failed: " + ex.Message, true); Log("❌ " + ex.Message); btnComplete.IsEnabled = true; }
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
