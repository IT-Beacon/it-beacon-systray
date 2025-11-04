using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace it_beacon_systray.Models
{
    // --- Models for Snipe-IT API ---
    public class SnipeItAssetResponse
    {
        public List<SnipeItAsset>? rows { get; set; }
    }

    public class SnipeItAsset
    {
        public string? name { get; set; }
        public SnipeItLocation? location { get; set; }
    }

    public class SnipeItLocation
    {
        public string? name { get; set; }
    }


    // --- Models for Risk Score API ---
    public class SyncedAssetsResponse
    {
        public List<SyncedAsset>? assets { get; set; }
    }

    public class SyncedAsset
    {
        [JsonPropertyName("nama")]
        public string Hostname { get; set; } = string.Empty;

        [JsonPropertyName("nilai")]
        public int RiskScore { get; set; }
    }

    // --- Model for NetworkInfo Helper ---
    public class NetworkInfo
    {
        public string DisplayIp { get; set; } = "N/A";
        public string Tooltip { get; set; } = "Unknown";
    }
}
