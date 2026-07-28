using QRCoder;

namespace TrackNTrash.LabelApi.Services;

/// <summary>
/// Cross-platform QR rendering via QRCoder (no System.Drawing dependency).
/// Error-correction policy:
///   * Cartons — level Q (~25%): good balance for printed labels.
///   * Trays   — level H (~30%, "2x"): highest correction for harsh warehouse / laser-etch use.
/// </summary>
public sealed class QrImageService
{
    private readonly QRCodeGenerator _generator = new();

    public string PngBase64(string payload, QRCodeGenerator.ECCLevel ecc, int pixelsPerModule = 10)
        => Convert.ToBase64String(Png(payload, ecc, pixelsPerModule));

    public byte[] Png(string payload, QRCodeGenerator.ECCLevel ecc, int pixelsPerModule = 10)
    {
        using var data = _generator.CreateQrCode(payload, ecc);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    /// <summary>SVG vector output (used for tray laser-etch at ECC level H).</summary>
    public string Svg(string payload, QRCodeGenerator.ECCLevel ecc, int pixelsPerModule = 10)
    {
        using var data = _generator.CreateQrCode(payload, ecc);
        return new SvgQRCode(data).GetGraphic(pixelsPerModule);
    }

    // Convenience wrappers encoding the per-artifact ECC policy.
    public string CartonPngBase64(string payload) => PngBase64(payload, QRCodeGenerator.ECCLevel.Q);
    public string TrayPngBase64(string payload)   => PngBase64(payload, QRCodeGenerator.ECCLevel.H);
    public string TraySvg(string payload)         => Svg(payload, QRCodeGenerator.ECCLevel.H);
}
