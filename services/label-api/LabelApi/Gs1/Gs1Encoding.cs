using System.Text;

namespace TrackNTrash.LabelApi.Gs1;

/// <summary>
/// Helpers for building GS1 element strings and GS1-QR data payloads.
///
/// Element string  — human-readable, AIs in parentheses:  (01)09501234567890(21)ABC123
/// QR data payload — machine-readable. A GS1-QR symbol is encoded in FNC1 mode; the leading
///   FNC1 is carried by the symbology (reader reports it as the "]Q3" identifier). Between two
///   variable-length AIs a GS (ASCII 29) separator is required. Fixed-length AIs (e.g. (01))
///   and a trailing variable AI (e.g. (21) last) need no separator.
/// </summary>
public static class Gs1Encoding
{
    /// <summary>GS1 group separator (FNC1 in data) — ASCII 29.</summary>
    public const char GroupSeparator = (char)29;

    public const string AiGtin   = "01"; // fixed length 14
    public const string AiSerial = "21"; // variable, up to 20
    public const string AiSscc   = "00"; // fixed length 18

    /// <summary>(01)GTIN(21)serial element string for a serialized carton.</summary>
    public static string CartonElementString(string gtin14, string serial)
        => $"({AiGtin}){gtin14}({AiSerial}){serial}";

    /// <summary>
    /// GS1-QR data payload for a serialized carton. (01) is fixed-length so it needs no
    /// trailing separator; (21) is last, so no separator is required after it either.
    /// </summary>
    public static string CartonQrPayload(string gtin14, string serial)
        => $"{AiGtin}{gtin14}{AiSerial}{serial}";

    /// <summary>(00)SSCC element string.</summary>
    public static string SsccElementString(string sscc18)
        => $"({AiSscc}){sscc18}";

    /// <summary>GS1-QR data payload for an SSCC. (00) is fixed-length (18) — no separator needed.</summary>
    public static string SsccQrPayload(string sscc18)
        => $"{AiSscc}{sscc18}";

    /// <summary>
    /// Renders a payload for logging/debugging with the GS group separator shown as {GS}.
    /// </summary>
    public static string ToPrintable(string payload)
        => payload.Replace(GroupSeparator.ToString(), "{GS}");

    /// <summary>Normalizes a GTIN input to 14 digits (left-pads shorter GTIN-8/12/13).</summary>
    public static string NormalizeGtin14(string gtin)
    {
        var digits = new string(gtin.Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 14)
            throw new ArgumentException($"GTIN '{gtin}' must be 8–14 digits.", nameof(gtin));
        return digits.PadLeft(14, '0');
    }
}
