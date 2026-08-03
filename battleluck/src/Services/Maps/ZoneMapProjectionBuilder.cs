using System;
using System.Collections.Generic;
using BattleLuck.Models.Maps;

namespace BattleLuck.Services.Maps;

/// <summary>
/// Builds an immutable zone map projection from live zone definitions.
/// Only primitive values are emitted; no ECS handles are persisted.
/// The builder is intentionally conservative: unknown or invalid zones are
/// skipped so a bad zone can never break the map projection.
/// </summary>
public sealed class ZoneMapProjectionBuilder
{
    readonly IZoneSource _zoneSource;

    public ZoneMapProjectionBuilder(IZoneSource zoneSource)
    {
        _zoneSource = zoneSource ?? throw new ArgumentNullException(nameof(zoneSource));
    }

    /// <summary>
    /// Produce the zone projection from the live zone source.
    /// </summary>
    public IReadOnlyList<ZoneMapRecord> Build()
    {
        if (_zoneSource == null)
            return Array.Empty<ZoneMapRecord>();

        var records = new List<ZoneMapRecord>();
        try
        {
            foreach (var zone in _zoneSource.GetZones())
            {
                if (zone == null || zone.ZoneHash == 0)
                    continue;

                records.Add(new ZoneMapRecord
                {
                    ZoneHash = zone.ZoneHash,
                    Name = zone.Name ?? string.Empty,
                    CenterX = zone.CenterX,
                    CenterZ = zone.CenterZ,
                    Radius = zone.Radius,
                    IsActive = zone.IsActive
                });
            }
        }
        catch
        {
            // A projection failure must never crash the server tick.
            return Array.Empty<ZoneMapRecord>();
        }

        return records;
    }

    /// <summary>
    /// Source for live zone data. Implementations may read from the session
    /// controller, zone detection system, or event config loader. Providing
    /// an interface keeps this builder independent of internal APIs that are
    /// still being stabilized.
    /// </summary>
    public interface IZoneSource
    {
        IReadOnlyList<ZoneInfo> GetZones();
    }

    public sealed record ZoneInfo(
        int ZoneHash,
        string Name,
        float CenterX,
        float CenterZ,
        float Radius,
        bool IsActive);
}