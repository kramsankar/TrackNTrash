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
        InitLanguage();
    }

    private async Task CheckHealth()
    {
        bool ok = await _api.HealthAsync();
        lblConn.Text = ok ? $"● {Loc.T("status.connected")} · {TrackApiClient.BaseUrl}" : $"○ {Loc.T("status.offline")}";
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

    // ---- Item-level counting -------------------------------------------------------
    // A carton has to exist in the database before its units can be counted, because the
    // count hangs off the carton id rather than the QR payload.

    private long? _cartonId;
    private readonly List<string> _units = new();

    private async void OnRegisterCarton(object? sender, EventArgs e)
    {
        if (_orderLineId is null) { Banner("Start an order first", true); return; }
        var serial = txtItemSerial.Text?.Trim() ?? "";
        if (serial.Length == 0) { Banner("Enter a carton serial", true); return; }
        if (!int.TryParse(txtItemExpected.Text, out var expectedUnits) || expectedUnits < 1)
        { Banner("Enter how many units the carton should hold", true); return; }

        btnRegisterCarton.IsEnabled = false;
        try
        {
            // Mixed: some units carry a barcode, some are counted by camera. That is the
            // normal case on a retail line, so it is the default rather than a special mode.
            _cartonId = await _api.CreateCartonAsync(_orderLineId.Value, txtGtin.Text.Trim(),
                serial, expectedUnits, "Mixed");
            _units.Clear();
            lblUnits.Text = "0 units scanned";
            lblCarton.Text = $"Carton {serial} registered (id {_cartonId}) · expects {expectedUnits} units";
            btnSubmitCount.IsEnabled = true;
            Banner($"Carton {serial} ready — scan its units", false);
            Log($"📦 Carton {serial} registered (id {_cartonId})");
        }
        catch (Exception ex) { Banner("Register failed: " + ex.Message, true); Log("❌ " + ex.Message); }
        finally { btnRegisterCarton.IsEnabled = true; }
    }

    private void OnUnitScanned(object? sender, EventArgs e)
    {
        var code = txtUnit.Text?.Trim() ?? "";
        txtUnit.Text = "";
        txtUnit.Focus();
        ProcessUnit(code);
    }

    private async void OnScanUnitCamera(object? sender, EventArgs e)
    {
        var page = new ScanPage();
        await Navigation.PushModalAsync(page);
        var code = await page.Result;
        if (!string.IsNullOrWhiteSpace(code)) ProcessUnit(code.Trim());
    }

    private void ProcessUnit(string code)
    {
        if (code.Length == 0) return;
        if (_cartonId is null) { Banner("Register a carton first", true); return; }
        // A repeated unit barcode is a double-scan, not a second unit.
        if (_units.Contains(code, StringComparer.OrdinalIgnoreCase))
        { Banner($"Unit already scanned: {code}", true); return; }

        _units.Add(code);
        lblUnits.Text = $"{_units.Count} units scanned";
        Banner($"✅ unit {code}", false);
    }

    private async void OnSubmitCount(object? sender, EventArgs e)
    {
        if (_cartonId is null) return;
        int? vision = int.TryParse(txtVisionCount.Text, out var v) && v >= 0 ? v : null;
        if (_units.Count == 0 && vision is null)
        { Banner("Scan some units or enter a visual count", true); return; }

        btnSubmitCount.IsEnabled = false;
        try
        {
            var r = await _api.CountItemsAsync(_cartonId.Value, _units, vision, DeviceId);
            // The API's verdicts are MATCH / SHORT / OVER / UNVERIFIED — only MATCH is clean.
            var ok = r?.Verdict == "MATCH";
            lblCountVerdict.Text = r is null ? "" : $"{r.Verdict} — {r.Detail}";
            lblCountVerdict.TextColor = Color.FromArgb(ok ? "#43b477" : "#f2a33c");
            Banner(ok ? $"Item count matches ({r!.Expected} units)" : $"⚠ {r?.Verdict}: {r?.Detail}", !ok);
            Log($"🔢 Item count on carton {_cartonId}: {r?.Verdict} ({r?.Scanned}/{r?.Expected}, vision {r?.Vision?.ToString() ?? "—"})");
        }
        catch (Exception ex) { Banner("Count failed: " + ex.Message, true); Log("❌ " + ex.Message); }
        finally { btnSubmitCount.IsEnabled = true; }
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
            ? $"✓ {Loc.T("signIn.signedAs")}: {_api.Auth.Username}"
            : Loc.T("signIn.hint");
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

    // ---- Language ------------------------------------------------------------------
    // Strings are compiled in, so switching is instant and works with no signal. Only the
    // sign-in card and status line are re-rendered here; the rest of this screen is scan
    // fields and codes, which stay as they are in every language.

    private bool _langReady;

    private void InitLanguage()
    {
        pickLang.ItemsSource = Loc.Languages.Select(l => l.Native).ToList();
        var idx = Array.FindIndex(Loc.Languages, l => l.Code == Loc.Current);
        pickLang.SelectedIndex = idx < 0 ? 0 : idx;
        _langReady = true;
        ApplyLanguage();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_langReady || pickLang.SelectedIndex < 0) return;
        Loc.Current = Loc.Languages[pickLang.SelectedIndex].Code;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        btnSignIn.Text = Loc.T("action.signIn");
        btnSignOut.Text = Loc.T("action.signOut");
        txtUser.Placeholder = Loc.T("signIn.username");
        txtPass.Placeholder = Loc.T("signIn.password");
        RefreshAuthUi();
    }
}
