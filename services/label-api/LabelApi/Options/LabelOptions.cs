namespace TrackNTrash.LabelApi.Options;

public sealed class LabelOptions
{
    public const string SectionName = "Label";

    /// <summary>"InMemory" (dev/test) or "Sql" (uses DB sequences).</summary>
    public string SerialProvider { get; set; } = "InMemory";

    /// <summary>Connection string used when SerialProvider = "Sql".</summary>
    public string? ConnectionString { get; set; }

    public SsccOptions Sscc { get; set; } = new();
    public ZplOptions Zpl { get; set; } = new();
}

public sealed class SsccOptions
{
    /// <summary>GS1 company prefix (digits). Length + extension + serial-ref = 17 data digits.</summary>
    public string CompanyPrefix { get; set; } = "0614141";

    /// <summary>SSCC extension digit 0–9 (logistics use). Default 0.</summary>
    public int ExtensionDigit { get; set; } = 0;
}

public sealed class ZplOptions
{
    public int Dpi { get; set; } = 203;      // Zebra 203 dpi
    public double WidthInches { get; set; } = 4;
    public double HeightInches { get; set; } = 6;
}
