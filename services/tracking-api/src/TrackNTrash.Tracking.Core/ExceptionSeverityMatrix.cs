namespace TrackNTrash.Tracking.Core;

/// <summary>
/// Configurable severity mapping per exception type. Defaults below can be overridden
/// (e.g. from configuration) by passing a dictionary to the constructor.
/// </summary>
public sealed class ExceptionSeverityMatrix
{
    private readonly IReadOnlyDictionary<ExceptionType, ExceptionSeverity> _map;

    private static readonly Dictionary<ExceptionType, ExceptionSeverity> Defaults = new()
    {
        { ExceptionType.CountMismatch,      ExceptionSeverity.High     },
        { ExceptionType.UnknownCarton,      ExceptionSeverity.High     },
        { ExceptionType.MissingCarton,      ExceptionSeverity.High     },
        { ExceptionType.WrongTrip,          ExceptionSeverity.Critical },
        { ExceptionType.WrongStore,         ExceptionSeverity.Critical },
        { ExceptionType.IllegalTransition,  ExceptionSeverity.Medium   },
        { ExceptionType.TrayDwellExceeded,  ExceptionSeverity.Low      },
        { ExceptionType.NoReceiveSla,       ExceptionSeverity.High     },
        { ExceptionType.SuspectedLost,      ExceptionSeverity.Medium   },
        { ExceptionType.Damaged,            ExceptionSeverity.High     },
        { ExceptionType.ShortShipped,       ExceptionSeverity.High     },
    };

    public ExceptionSeverityMatrix(IReadOnlyDictionary<ExceptionType, ExceptionSeverity>? overrides = null)
    {
        if (overrides is null || overrides.Count == 0)
        {
            _map = Defaults;
            return;
        }
        var merged = new Dictionary<ExceptionType, ExceptionSeverity>(Defaults);
        foreach (var kv in overrides) merged[kv.Key] = kv.Value;
        _map = merged;
    }

    public ExceptionSeverity For(ExceptionType type)
        => _map.TryGetValue(type, out var s) ? s : ExceptionSeverity.Medium;
}
