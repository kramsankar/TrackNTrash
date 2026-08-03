using System.Globalization;

namespace TrackNTrash.DriverApp.Services;

/// <summary>
/// On-device localisation. Tamil and Hindi to start.
///
/// Strings are compiled in, not fetched. A picker on a warehouse floor works in and out of
/// Wi-Fi coverage all shift, and a handheld whose buttons only appear once a call succeeds is
/// worse than useless — it fails exactly when the network does. Data values (shipment states,
/// exception types) still come from the API, which is where they live.
///
/// A missing string falls back to English rather than showing a key. Half-translated is
/// usable; "action.completeTray" on a button is not.
/// </summary>
public static class Loc
{
    public const string StorageKey = "tnt.lang";

    public sealed record LangOption(string Code, string Native, string English);

    public static readonly LangOption[] Languages =
    {
        new("en", "English", "English"),
        new("ta", "\u0BA4\u0BAE\u0BBF\u0BB4\u0BCD", "Tamil"),
        new("hi", "\u0939\u093F\u0928\u094D\u0926\u0940", "Hindi"),
    };

    private static string _current = "";

    /// <summary>Current language: the stored choice, else the device locale, else English.</summary>
    public static string Current
    {
        get
        {
            if (!string.IsNullOrEmpty(_current)) return _current;
            var stored = Preferences.Default.Get<string?>(StorageKey, null);
            if (!string.IsNullOrWhiteSpace(stored) && Strings.ContainsKey(stored)) return _current = stored;
            // A handset already set to Tamil should open in Tamil without anyone being told
            // there is a setting to change.
            var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return _current = Strings.ContainsKey(device) ? device : "en";
        }
        set
        {
            _current = Strings.ContainsKey(value) ? value : "en";
            Preferences.Default.Set(StorageKey, _current);
        }
    }

    public static string NativeName(string code) =>
        Languages.FirstOrDefault(l => l.Code == code)?.Native ?? code;

    /// <summary>Translate a key. Unknown keys fall back to English, then to the key itself.</summary>
    public static string T(string key)
    {
        if (Strings.TryGetValue(Current, out var bundle) && bundle.TryGetValue(key, out var v)) return v;
        if (Strings["en"].TryGetValue(key, out var en)) return en;
        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["en"] = new()
        {
            ["signIn.title"]     = "SIGN IN",
            ["signIn.hint"]      = "The tracking API only accepts scans from a signed-in user.",
            ["signIn.signedAs"]  = "Signed in as",
            ["signIn.username"]  = "Username",
            ["signIn.password"]  = "Password",
            ["action.signIn"]    = "Sign in",
            ["action.signOut"]   = "Sign out",
            ["label.language"]   = "Language",
            ["status.connected"] = "Connected",
            ["status.offline"]   = "Offline \u2014 cannot reach the API",
            ["error.credentials"] = "Wrong username or password.",
            ["error.enterBoth"]  = "Enter a username and password.",
        },
        ["ta"] = new()
        {
            ["signIn.title"]     = "\u0BB2\u0BCB\u0B95\u0BBF\u0BA9\u0BCD",
            ["signIn.hint"]      = "\u0B89\u0BB3\u0BCD\u0BA8\u0BC1\u0BB4\u0BC8\u0BA8\u0BCD\u0BA4 \u0BAA\u0BAF\u0BA9\u0BB0\u0BCD\u0B95\u0BB3\u0BCD \u0BAE\u0B9F\u0BCD\u0B9F\u0BC1\u0BAE\u0BC7 \u0BB8\u0BCD\u0B95\u0BC7\u0BA9\u0BCD \u0B9A\u0BC6\u0BAF\u0BCD\u0BAF \u0BAE\u0BC1\u0B9F\u0BBF\u0BAF\u0BC1\u0BAE\u0BCD.",
            ["signIn.signedAs"]  = "\u0B89\u0BB3\u0BCD\u0BA8\u0BC1\u0BB4\u0BC8\u0BA8\u0BCD\u0BA4\u0BB5\u0BB0\u0BCD",
            ["signIn.username"]  = "\u0BAA\u0BAF\u0BA9\u0BB0\u0BCD \u0BAA\u0BC6\u0BAF\u0BB0\u0BCD",
            ["signIn.password"]  = "\u0B95\u0B9F\u0BB5\u0BC1\u0B9A\u0BCD\u0B9A\u0BCA\u0BB2\u0BCD",
            ["action.signIn"]    = "\u0B89\u0BB3\u0BCD\u0BA8\u0BC1\u0BB4\u0BC8",
            ["action.signOut"]   = "\u0BB5\u0BC6\u0BB3\u0BBF\u0BAF\u0BC7\u0BB1\u0BC1",
            ["label.language"]   = "\u0BAE\u0BCA\u0BB4\u0BBF",
            ["status.connected"] = "\u0B87\u0BA3\u0BC8\u0B95\u0BCD\u0B95\u0BAA\u0BCD\u0BAA\u0B9F\u0BCD\u0B9F\u0BA4\u0BC1",
            ["status.offline"]   = "\u0B87\u0BA3\u0BC8\u0BAA\u0BCD\u0BAA\u0BC1 \u0B87\u0BB2\u0BCD\u0BB2\u0BC8",
            ["error.credentials"] = "\u0BAA\u0BAF\u0BA9\u0BB0\u0BCD \u0BAA\u0BC6\u0BAF\u0BB0\u0BCD \u0B85\u0BB2\u0BCD\u0BB2\u0BA4\u0BC1 \u0B95\u0B9F\u0BB5\u0BC1\u0B9A\u0BCD\u0B9A\u0BCA\u0BB2\u0BCD \u0BA4\u0BB5\u0BB1\u0BC1.",
            ["error.enterBoth"]  = "\u0BAA\u0BAF\u0BA9\u0BB0\u0BCD \u0BAA\u0BC6\u0BAF\u0BB0\u0BCD \u0BAE\u0BB1\u0BCD\u0BB1\u0BC1\u0BAE\u0BCD \u0B95\u0B9F\u0BB5\u0BC1\u0B9A\u0BCD\u0B9A\u0BCA\u0BB2\u0BCD \u0B89\u0BB3\u0BCD\u0BB3\u0BBF\u0B9F\u0BCD\u0B95.",
        },
        ["hi"] = new()
        {
            ["signIn.title"]     = "\u0938\u093E\u0907\u0928 \u0907\u0928",
            ["signIn.hint"]      = "\u091F\u094D\u0930\u0948\u0915\u093F\u0902\u0917 API \u0915\u0947\u0935\u0932 \u0938\u093E\u0907\u0928 \u0907\u0928 \u0909\u092A\u092F\u094B\u0917\u0915\u0930\u094D\u0924\u093E \u0938\u0947 \u0938\u094D\u0915\u0948\u0928 \u0938\u094D\u0935\u0940\u0915\u093E\u0930 \u0915\u0930\u0924\u093E \u0939\u0948\u0964",
            ["signIn.signedAs"]  = "\u0938\u093E\u0907\u0928 \u0907\u0928",
            ["signIn.username"]  = "\u0909\u092A\u092F\u094B\u0917\u0915\u0930\u094D\u0924\u093E \u0928\u093E\u092E",
            ["signIn.password"]  = "\u092A\u093E\u0938\u0935\u0930\u094D\u0921",
            ["action.signIn"]    = "\u0938\u093E\u0907\u0928 \u0907\u0928",
            ["action.signOut"]   = "\u0938\u093E\u0907\u0928 \u0906\u0909\u091F",
            ["label.language"]   = "\u092D\u093E\u0937\u093E",
            ["status.connected"] = "\u091C\u0941\u0921\u093C\u093E \u0939\u0941\u0906",
            ["status.offline"]   = "\u0911\u092B\u093C\u0932\u093E\u0907\u0928",
            ["error.credentials"] = "\u0909\u092A\u092F\u094B\u0917\u0915\u0930\u094D\u0924\u093E \u0928\u093E\u092E \u092F\u093E \u092A\u093E\u0938\u0935\u0930\u094D\u0921 \u0917\u0932\u0924 \u0939\u0948\u0964",
            ["error.enterBoth"]  = "\u0909\u092A\u092F\u094B\u0917\u0915\u0930\u094D\u0924\u093E \u0928\u093E\u092E \u0914\u0930 \u092A\u093E\u0938\u0935\u0930\u094D\u0921 \u0926\u0930\u094D\u091C \u0915\u0930\u0947\u0902\u0964",
        },
    };
}
