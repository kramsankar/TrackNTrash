using TrackNTrash.LabelApi.Gs1;
using TrackNTrash.LabelApi.Models;
using TrackNTrash.LabelApi.Options;

namespace TrackNTrash.LabelApi.Services;

/// <summary>
/// Orchestrates label generation for cartons, SSCCs and trays. Combines the GS1
/// encoding/check-digit helpers, the serial provider and the QR/ZPL renderers.
/// </summary>
public sealed class LabelService
{
    private readonly ISerialNumberProvider _serials;
    private readonly QrImageService _qr;
    private readonly ZplRenderer _zpl;
    private readonly SsccOptions _sscc;

    public LabelService(
        ISerialNumberProvider serials,
        QrImageService qr,
        ZplRenderer zpl,
        SsccOptions ssccOptions)
    {
        _serials = serials;
        _qr = qr;
        _zpl = zpl;
        _sscc = ssccOptions;
    }

    // ---------------- Cartons ----------------

    public async Task<IReadOnlyList<CartonLabel>> CreateCartonLabelsAsync(
        CartonLabelRequest req, CancellationToken ct = default)
    {
        if (req.Quantity is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(req.Quantity), "Quantity must be 1..1000.");

        string gtin14 = Gs1Encoding.NormalizeGtin14(req.Gtin);
        if (!Gs1CheckDigit.IsValid(gtin14))
            throw new ArgumentException($"GTIN '{req.Gtin}' has an invalid check digit.", nameof(req.Gtin));

        var labels = new List<CartonLabel>(req.Quantity);
        for (int i = 0; i < req.Quantity; i++)
        {
            long seq = await _serials.NextCartonSerialAsync(ct);
            // Serial: 12-digit zero-padded reference (numeric, ≤20 alphanumeric constraint honored).
            string serial = seq.ToString("D12");

            string element = Gs1Encoding.CartonElementString(gtin14, serial);
            string payload = Gs1Encoding.CartonQrPayload(gtin14, serial);

            labels.Add(new CartonLabel
            {
                Serial = serial,
                Gtin = gtin14,
                Gs1ElementString = element,
                QrPayload = payload,
                PngBase64 = _qr.CartonPngBase64(payload),
                Zpl = req.IncludeZpl ? _zpl.CartonZpl(payload, gtin14, serial) : null
            });
        }
        return labels;
    }

    // ---------------- SSCC ----------------

    public async Task<IReadOnlyList<SsccLabel>> CreateSsccLabelsAsync(
        SsccLabelRequest req, CancellationToken ct = default)
    {
        if (req.Quantity is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(req.Quantity), "Quantity must be 1..1000.");

        string prefix = new string(_sscc.CompanyPrefix.Where(char.IsDigit).ToArray());
        if (_sscc.ExtensionDigit is < 0 or > 9)
            throw new InvalidOperationException("SSCC ExtensionDigit must be 0..9.");
        // 1 extension + prefix + serial reference = 17 data digits (+1 check = 18).
        int serialRefLen = 17 - 1 - prefix.Length;
        if (serialRefLen < 1)
            throw new InvalidOperationException(
                $"Company prefix '{prefix}' is too long to leave room for a serial reference.");

        var labels = new List<SsccLabel>(req.Quantity);
        for (int i = 0; i < req.Quantity; i++)
        {
            long seq = await _serials.NextSsccReferenceAsync(ct);
            string serialRef = seq.ToString().PadLeft(serialRefLen, '0');
            if (serialRef.Length > serialRefLen)
                throw new InvalidOperationException("SSCC serial reference sequence overflowed the prefix budget.");

            string data17 = $"{_sscc.ExtensionDigit}{prefix}{serialRef}";
            string sscc18 = Gs1CheckDigit.BuildSscc18(data17);

            string element = Gs1Encoding.SsccElementString(sscc18);
            string payload = Gs1Encoding.SsccQrPayload(sscc18);

            labels.Add(new SsccLabel
            {
                Sscc = sscc18,
                Gs1ElementString = element,
                QrPayload = payload,
                PngBase64 = _qr.CartonPngBase64(payload), // SSCC on cartons → ECC Q
                Zpl = req.IncludeZpl ? _zpl.SsccZpl(payload, sscc18) : null
            });
        }
        return labels;
    }

    // ---------------- Trays ----------------

    public async Task<IReadOnlyList<TrayLabel>> CreateTrayLabelsAsync(
        TrayLabelRequest req, CancellationToken ct = default)
    {
        if (req.Quantity is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(req.Quantity), "Quantity must be 1..1000.");
        string site = new string(req.SiteCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (site.Length == 0)
            throw new ArgumentException("SiteCode is required.", nameof(req.SiteCode));

        var labels = new List<TrayLabel>(req.Quantity);
        for (int i = 0; i < req.Quantity; i++)
        {
            int seq = await _serials.NextTraySequenceAsync(ct);
            string trayQr = $"TRAY-{site}-{seq:D6}";

            labels.Add(new TrayLabel
            {
                TrayQr = trayQr,
                PngBase64 = _qr.TrayPngBase64(trayQr),                       // ECC H
                Svg = req.LaserEtchSvg ? _qr.TraySvg(trayQr) : null,         // ECC H vector
                Zpl = req.IncludeZpl ? _zpl.TrayZpl(trayQr, trayQr) : null
            });
        }
        return labels;
    }
}
