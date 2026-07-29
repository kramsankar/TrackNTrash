using TrackNTrash.ReceivingApp.Services;

namespace TrackNTrash.ReceivingApp;

public partial class MainPage : ContentPage
{
    private readonly TrackApiClient _api = new();
    private string? _sessionId;
    private const string DeviceId = "RECV-DESKTOP";

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

    private async void OnStart(object? sender, EventArgs e)
    {
        var payloads = (txtExpected.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (payloads.Length == 0) { Banner("Enter at least one expected carton", true); return; }
        btnStart.IsEnabled = false;
        try
        {
            await _api.UpsertAsnAsync(txtTray.Text.Trim(), txtStore.Text.Trim(), payloads);
            var s = await _api.StartAsync(txtTray.Text.Trim(), txtStore.Text.Trim());
            if (s is null) { Banner("Could not start (no ASN)", true); return; }
            _sessionId = s.SessionId;
            lblSession.Text = $"Session {s.SessionId} · expecting {s.Expected} cartons";
            lblTally.Text = $"0 / {s.Expected} received";
            Banner($"Receiving {s.Expected} cartons for {s.StoreCode}", false);
            Log($"✅ started session {s.SessionId}");
            txtScan.Focus();
        }
        catch (Exception ex) { Banner("Failed: " + ex.Message, true); Log("❌ " + ex.Message); }
        finally { btnStart.IsEnabled = true; }
    }

    // Camera scan (phone targets): scan a carton QR and receive it.
    private async void OnScanCamera(object? sender, EventArgs e)
    {
        var page = new ScanPage();
        await Navigation.PushModalAsync(page);
        var code = await page.Result;
        if (!string.IsNullOrWhiteSpace(code)) { txtScan.Text = code.Trim(); OnScan(this, EventArgs.Empty); }
    }

    private async void OnScan(object? sender, EventArgs e)
    {
        var payload = txtScan.Text?.Trim() ?? "";
        txtScan.Text = "";
        txtScan.Focus();
        if (payload.Length == 0) return;
        if (_sessionId is null) { Banner("Start receiving first", true); return; }
        try
        {
            var r = await _api.ScanAsync(_sessionId, payload);
            if (r is null) return;
            lblTally.Text = $"{r.Received} / {r.Expected} received · {r.Unexpected} over";
            switch (r.Outcome)
            {
                case "Received": Banner($"✅ {payload}", false); break;
                case "Duplicate": Banner($"Already received {payload}", false); break;
                case "Over": Banner(r.CorrectStoreCode is not null ? $"⛔ OVER — belongs to {r.CorrectStoreCode}" : "⛔ OVER — unknown carton", true); break;
            }
            btnComplete.IsEnabled = true;
            Log($"{r.Outcome}: {payload}");
        }
        catch (Exception ex) { Banner("Scan failed: " + ex.Message, true); Log("❌ " + ex.Message); }
    }

    private async void OnComplete(object? sender, EventArgs e)
    {
        if (_sessionId is null) return;
        if (string.IsNullOrWhiteSpace(txtReceiver.Text)) { Banner("Receiver name required", true); return; }
        btnComplete.IsEnabled = false;
        try
        {
            var sum = await _api.CompleteAsync(_sessionId, DeviceId, txtReceiver.Text.Trim());
            var shorts = sum is null || sum.ShortPayloads.Count == 0 ? "none" : string.Join(", ", sum.ShortPayloads);
            Banner(sum?.Clean == true ? "✅ Clean delivery — complete" : $"⚠️ Complete · short: {shorts}", sum?.Clean != true);
            Log($"🏁 complete: received {sum?.ReceivedCount}/{sum?.ExpectedCount}, short=[{shorts}], over={sum?.OverPayloads.Count}");
            _sessionId = null;
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
