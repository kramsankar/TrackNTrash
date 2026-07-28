using TrackNTrash.LabelApi.Options;

namespace TrackNTrash.LabelApi.Services;

/// <summary>
/// Renders ZPL II for Zebra printers. Default template: 4x6 inch @ 203 dpi,
/// a GS1-QR symbol (^BQ) and human-readable text below the code.
/// GS1-QR data in ZPL uses the "QA," mixed-mode prefix on the ^FD field.
/// </summary>
public sealed class ZplRenderer
{
    private readonly ZplOptions _opts;

    public ZplRenderer(ZplOptions opts) => _opts = opts;

    private int WidthDots  => (int)(_opts.WidthInches  * _opts.Dpi);
    private int HeightDots => (int)(_opts.HeightInches * _opts.Dpi);

    /// <summary>Carton label: GS1-QR + human-readable GTIN and serial.</summary>
    public string CartonZpl(string qrPayload, string gtin14, string serial)
    {
        return Build(
            qrPayload,
            magnification: 6,
            line1: $"GTIN: {gtin14}",
            line2: $"SERIAL: {serial}");
    }

    /// <summary>SSCC label: GS1-QR + human-readable SSCC.</summary>
    public string SsccZpl(string qrPayload, string sscc18)
    {
        return Build(
            qrPayload,
            magnification: 6,
            line1: "SSCC:",
            line2: FormatSscc(sscc18));
    }

    /// <summary>Tray asset label: high-magnification QR + tray code.</summary>
    public string TrayZpl(string qrPayload, string trayQr)
    {
        return Build(
            qrPayload,
            magnification: 8,
            line1: "TRAY",
            line2: trayQr);
    }

    private string Build(string qrData, int magnification, string line1, string line2)
    {
        // ^PW print width, ^LL label length, ^BQ QR, ^A0 font, ^FO field origin (dots).
        int cx = WidthDots / 2;
        return
$@"^XA
^PW{WidthDots}
^LL{HeightDots}
^CI28
^FO60,80^BQN,2,{magnification}^FDQA,{qrData}^FS
^CF0,60
^FO60,{HeightDots - 260}^FB{WidthDots - 120},1,0,C^FD{Escape(line1)}^FS
^CF0,50
^FO60,{HeightDots - 180}^FB{WidthDots - 120},2,0,C^FD{Escape(line2)}^FS
^XZ";
    }

    private static string FormatSscc(string sscc18)
        => sscc18.Length == 18
            ? $"({sscc18[..2]}) {sscc18[2..]}"
            : sscc18;

    // Escape ZPL control characters that could break the field.
    private static string Escape(string s)
        => s.Replace("^", " ").Replace("~", " ");
}
