using System;
using BattleLuck.Models.Maps;

namespace BattleLuck.Services.Maps;

/// <summary>
/// Server-side map projection orchestrator. Called once per tick from the
/// main thread; captures an immutable snapshot at most every 2 seconds.
/// Uses a timestamp threshold (<see cref="_nextCaptureUtc"/>) instead of a
/// modulo tick counter so capture cadence is independent of tick rate.
/// The server does not render anything — CotrexBL consumes the snapshot.
/// </summary>
public sealed class MapProjectionService
{
    readonly CastleMapProjectionBuilder _castleBuilder;
    readonly ZoneMapProjectionBuilder _zoneBuilder;
    readonly MapSnapshotStore _store;
    DateTime _nextCaptureUtc = DateTime.MinValue;

    /// <summary>Capture interval in seconds.</summary>
    public const double CaptureIntervalSeconds = 2.0;

    public MapProjectionService(
        CastleMapProjectionBuilder castleBuilder,
        ZoneMapProjectionBuilder zoneBuilder,
        MapSnapshotStore store)
    {
        _castleBuilder = castleBuilder ?? throw new ArgumentNullException(nameof(castleBuilder));
        _zoneBuilder = zoneBuilder ?? throw new ArgumentNullException(nameof(zoneBuilder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Called from the server tick (main thread). Returns true when a new
    /// snapshot was published, false when still within the capture window.
    /// </summary>
    public bool Tick()
    {
        // Corrected plan: timestamp-based cadence, not modulo timing.
        if (DateTime.UtcNow < _nextCaptureUtc)
            return false;

        _nextCaptureUtc = DateTime.UtcNow.AddSeconds(CaptureIntervalSeconds);

        var snapshot = new MapSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow,
            Castles = _castleBuilder.Build(),
            Zones = _zoneBuilder.Build(),
            Resources = Array.Empty<ResourceMapRecord>(),
            Waygates = Array.Empty<WaygateMapRecord>()
        };

        _store.Replace(snapshot);
        return true;
    }

    /// <summary>
    /// Access the latest immutable snapshot. Consumers (e.g. a local HTTP
    /// sidecar) always read a consistent copy; no ECS handles are exposed.
    /// </summary>
    public MapSnapshot Current => _store.Current;

    /// <summary>Force the next capture on the following tick. Used on world load.</summary>
    public void Invalidate()
    {
        _nextCaptureUtc = DateTime.MinValue;
    }
}