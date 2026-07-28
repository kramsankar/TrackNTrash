namespace TrackNTrash.Tracking.Core.Receiving;

/// <summary>A carton expected in a tray per the ASN (advance shipping notice).</summary>
public sealed record ExpectedCarton
{
    public string Payload { get; init; } = "";     // carton QR payload (01..21..)
    public long OrderLineId { get; init; }
    public string? Gtin { get; init; }
}

/// <summary>Advance shipping notice: what a tray should contain for a given store.</summary>
public sealed record Asn
{
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public IReadOnlyList<ExpectedCarton> ExpectedCartons { get; init; } = Array.Empty<ExpectedCarton>();
}

public enum CartonReceiveOutcome { Received, Duplicate, Over, Damaged }

public sealed record CartonScanResult
{
    public CartonReceiveOutcome Outcome { get; init; }
    public string Payload { get; init; } = "";
    /// <summary>For Over: the store this carton actually belongs to (if resolvable).</summary>
    public string? CorrectStoreCode { get; init; }
    public string Message { get; init; } = "";
    public int Received { get; init; }
    public int Expected { get; init; }
    public int Unexpected { get; init; }
}

public sealed record ReceivingSummary
{
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public int ExpectedCount { get; init; }
    public int ReceivedCount { get; init; }
    public IReadOnlyList<string> ShortPayloads { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OverPayloads { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DamagedPayloads { get; init; } = Array.Empty<string>();
    public string? ReceiverName { get; init; }
    public bool Clean => ShortPayloads.Count == 0 && OverPayloads.Count == 0 && DamagedPayloads.Count == 0;
}

/// <summary>Proof-of-delivery captured at completion.</summary>
public sealed record ProofOfDelivery
{
    public string ReceiverName { get; init; } = "";
    public string? SignatureBlobUri { get; init; }
    public string? DeliveryPhotoBlobUri { get; init; }
}

/// <summary>Mutable state for one tray-receiving session on the handheld.</summary>
public sealed class ReceivingSession
{
    public required Asn Asn { get; init; }
    public HashSet<string> Received { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Over { get; } = new();
    public HashSet<string> Damaged { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int ExpectedCount => Asn.ExpectedCartons.Count;
    public bool IsExpected(string payload) => Asn.ExpectedCartons.Any(c =>
        string.Equals(c.Payload, payload, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<string> ShortPayloads => Asn.ExpectedCartons
        .Select(c => c.Payload)
        .Where(p => !Received.Contains(p));
}
