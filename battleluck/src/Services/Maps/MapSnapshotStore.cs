using System;
using System.Threading;
using BattleLuck.Models.Maps;

namespace BattleLuck.Services.Maps;

/// <summary>
/// Thread-safe immutable snapshot store. Producers replace the snapshot
/// atomically via <see cref="Replace"/>, consumers read a consistent
/// immutable copy via <see cref="Current"/>. The store never mutates a
/// snapshot after it is published.
/// </summary>
public sealed class MapSnapshotStore
{
    long _revision;
    MapSnapshot _current = new() { Revision = 0, CapturedAtUtc = DateTime.MinValue };

    public MapSnapshot Current
    {
        get
        {
            var snapshot = Volatile.Read(ref _current);
            return snapshot ?? new MapSnapshot { Revision = 0, CapturedAtUtc = DateTime.MinValue };
        }
    }

    public long Replace(MapSnapshot replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));

        var revision = Interlocked.Increment(ref _revision);
        var published = new MapSnapshot
        {
            Revision = revision,
            CapturedAtUtc = replacement.CapturedAtUtc,
            Castles = replacement.Castles,
            Zones = replacement.Zones,
            Resources = replacement.Resources,
            Waygates = replacement.Waygates
        };

        Volatile.Write(ref _current, published);
        return revision;
    }

    public void Reset()
    {
        Volatile.Write(ref _current, new MapSnapshot { Revision = 0, CapturedAtUtc = DateTime.MinValue });
        Interlocked.Exchange(ref _revision, 0);
    }
}