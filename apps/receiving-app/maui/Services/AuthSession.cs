using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrackNTrash.ReceivingApp.Services;

/// <summary>
/// Holds the signed-in user's bearer token and puts it on every API call.
///
/// The tracking API used to accept scans anonymously; now that the operational endpoints
/// are guarded, a device that cannot sign in cannot post, so the token is persisted and
/// restored on launch to keep the scanner usable across app restarts.
/// </summary>
public sealed class AuthSession
{
    private const string TokenKey = "tnt.token";
    private const string UserKey  = "tnt.username";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string? Token { get; private set; }
    public string? Username { get; private set; }
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(Token);

    public AuthSession()
    {
        Token = Preferences.Default.Get<string?>(TokenKey, null);
        Username = Preferences.Default.Get<string?>(UserKey, null);
    }

    /// <summary>Returns null on success, or a message to show the user.</summary>
    public async Task<string?> SignInAsync(HttpClient http, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return "Enter a username and password.";
        try
        {
            var resp = await http.PostAsJsonAsync("/auth/login", new { username, password }, Json);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "Wrong username or password.";
            if (!resp.IsSuccessStatusCode)
                return $"Sign-in failed ({(int)resp.StatusCode}).";

            var body = await resp.Content.ReadFromJsonAsync<LoginResp>(Json);
            if (string.IsNullOrWhiteSpace(body?.Token)) return "The server returned no token.";

            Token = body!.Token;
            Username = username;
            Preferences.Default.Set(TokenKey, Token);
            Preferences.Default.Set(UserKey, Username);
            Apply(http);
            return null;
        }
        catch (Exception ex) { return "Cannot reach the API: " + ex.Message; }
    }

    public void SignOut(HttpClient http)
    {
        Token = null;
        Username = null;
        Preferences.Default.Remove(TokenKey);
        Preferences.Default.Remove(UserKey);
        http.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>Puts the restored token back on the client after a cold start.</summary>
    public void Apply(HttpClient http)
        => http.DefaultRequestHeaders.Authorization =
            IsSignedIn ? new AuthenticationHeaderValue("Bearer", Token) : null;

    private sealed record LoginResp(string? Token, string? DisplayName, string? Roles);
}
