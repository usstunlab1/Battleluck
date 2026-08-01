using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BattleLuck.Models
{
    public class PrefabArchive
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("prefabCount")]
        public int PrefabCount { get; set; }

        [JsonPropertyName("prefabs")]
        public Dictionary<string, PrefabArchiveEntry> Prefabs { get; set; } = new();
    }

    public class PrefabArchiveEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("flags")]
        public Dictionary<string, bool> Flags { get; set; } = new();

        [JsonPropertyName("aabbMin")]
        public float[]? AabbMin { get; set; }

        [JsonPropertyName("aabbMax")]
        public float[]? AabbMax { get; set; }

        /// <summary>
        /// Native tile-grid footprint relative to the prefab's tile anchor.
        /// The archive is a read-only editor aid; the live prefab map remains
        /// authoritative before a placement is spawned.
        /// </summary>
        [JsonPropertyName("tileBoundsMin")]
        public int[]? TileBoundsMin { get; set; }

        [JsonPropertyName("tileBoundsMax")]
        public int[]? TileBoundsMax { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }
    }
}
