namespace TrackNTrash.LabelApi.Models;

// ---------- Carton ----------

public sealed record CartonLabelRequest
{
    /// <summary>GTIN (8–14 digits; normalized to GTIN-14).</summary>
    public string Gtin { get; init; } = "";
    /// <summary>Order line reference (for traceability / print header).</summary>
    public string? OrderLineReference { get; init; }
    /// <summary>Number of serialized carton labels to generate.</summary>
    public int Quantity { get; init; } = 1;
    /// <summary>Also emit a Zebra ZPL payload for each label.</summary>
    public bool IncludeZpl { get; init; } = false;
}

public sealed record CartonLabel
{
    public string Serial { get; init; } = "";
    public string Gtin { get; init; } = "";
    public string Gs1ElementString { get; init; } = "";
    public string QrPayload { get; init; } = "";
    public string PngBase64 { get; init; } = "";
    public string? Zpl { get; init; }
}

// ---------- SSCC ----------

public sealed record SsccLabelRequest
{
    /// <summary>Number of SSCC-18 labels to generate.</summary>
    public int Quantity { get; init; } = 1;
    public bool IncludeZpl { get; init; } = false;
}

public sealed record SsccLabel
{
    public string Sscc { get; init; } = "";
    public string Gs1ElementString { get; init; } = "";
    public string QrPayload { get; init; } = "";
    public string PngBase64 { get; init; } = "";
    public string? Zpl { get; init; }
}

// ---------- Tray ----------

public sealed record TrayLabelRequest
{
    /// <summary>Site code embedded in the tray QR: TRAY-{siteCode}-{seq}.</summary>
    public string SiteCode { get; init; } = "";
    public int Quantity { get; init; } = 1;
    /// <summary>Emit an SVG vector (for laser etching) at high error correction.</summary>
    public bool LaserEtchSvg { get; init; } = false;
    public bool IncludeZpl { get; init; } = false;
}

public sealed record TrayLabel
{
    public string TrayQr { get; init; } = "";
    public string PngBase64 { get; init; } = "";
    public string? Svg { get; init; }
    public string? Zpl { get; init; }
}
