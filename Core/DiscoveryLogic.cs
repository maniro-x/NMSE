using System.Text.Json;
using System.Text.Json.Serialization;
using NMSE.Config;
using NMSE.Data;
using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Handles parsing of DiscoveryManagerData records from the save file,
/// and persistence of user-saved discoveries to a separate JSON file.
/// </summary>
internal static class DiscoveryLogic
{
    /// <summary>Parsed discovery record for display.</summary>
    internal readonly record struct DiscoveryRecord(
        string DiscoveryType,
        string DiscoveredBy,
        string Platform,
        string Timestamp,
        string GalaxyName,
        int RealityIndex,
        string PortalHex,
        string CustomName);

    /// <summary>
    /// A single user-saved discovery entry persisted to NMSE_Saved_Discoveries.json.
    /// </summary>
    internal class SavedDiscoveryEntry
    {
        public string DiscoveryType { get; set; } = "";
        public string DiscoveredBy { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string GalaxyName { get; set; } = "";
        public int RealityIndex { get; set; } = -1;
        public string PortalHex { get; set; } = "";
        public string CustomName { get; set; } = "";
        public string SaveName { get; set; } = "";
        public string SaveUniversalId { get; set; } = "";

        /// <summary>User-editable label for this entry (defaults to CustomName on copy).</summary>
        public string UserLabel { get; set; } = "";
    }

    private const string SavedDiscoveriesFileName = "NMSE_Saved_Discoveries.json";

    /// <summary>
    /// Navigates to DiscoveryManagerData.DiscoveryData-v1.Store.Record and returns the array.
    /// </summary>
    internal static JsonArray? FindDiscoveryRecords(JsonObject saveData)
    {
        var dmData = saveData.GetObject("DiscoveryManagerData");
        if (dmData == null) return null;
        var ddv1 = dmData.GetObject("DiscoveryData-v1");
        if (ddv1 == null) return null;
        var store = ddv1.GetObject("Store");
        if (store == null) return null;
        return store.GetArray("Record");
    }

    /// <summary>
    /// Navigates to DiscoveryManagerData.DiscoveryData-v1.Available and returns the array.
    /// Available discoveries have the same record structure as Stored (DD, TS, OWS).
    /// </summary>
    internal static JsonArray? FindAvailableRecords(JsonObject saveData)
    {
        var dmData = saveData.GetObject("DiscoveryManagerData");
        if (dmData == null) return null;
        var ddv1 = dmData.GetObject("DiscoveryData-v1");
        if (ddv1 == null) return null;
        return ddv1.GetArray("Available");
    }

    /// <summary>
    /// Resolves the player's display name from the save file.
    /// Reads <c>CommonStateData.UsedDiscoveryOwnersV2[0].USN</c>, falling back
    /// to persistent base owners if the discovery owners array is missing.
    /// </summary>
    internal static string GetPlayerName(JsonObject saveData)
    {
        try
        {
            var commonState = saveData.GetObject("CommonStateData");
            var owners = commonState?.GetArray("UsedDiscoveryOwnersV2");
            if (owners != null && owners.Length > 0)
            {
                string? usn = owners.GetObject(0)?.GetString("USN");
                if (!string.IsNullOrEmpty(usn)) return usn;
            }

            // Fallback: try persistent base owners
            var playerState = saveData.GetObject("PlayerStateData");
            var bases = playerState?.GetArray("PersistentPlayerBases");
            if (bases != null)
            {
                for (int i = 0; i < bases.Length; i++)
                {
                    try
                    {
                        string? usn = bases.GetObject(i)?.GetObject("Owner")?.GetString("USN");
                        if (!string.IsNullOrEmpty(usn)) return usn;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return "";
    }

    /// <summary>
    /// Returns the save name from <c>CommonStateData.SaveName</c>.
    /// </summary>
    internal static string GetSaveName(JsonObject saveData)
    {
        try
        {
            return saveData.GetObject("CommonStateData")?.GetString("SaveName") ?? "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// Returns a stable identifier for the save slot.
    /// Uses <c>CommonStateData.UsedDiscoveryOwnersV2[0].UID</c> if available,
    /// as this uniquely identifies a save across renames.
    /// </summary>
    internal static string GetSaveUniversalId(JsonObject saveData)
    {
        try
        {
            var commonState = saveData.GetObject("CommonStateData");
            var owners = commonState?.GetArray("UsedDiscoveryOwnersV2");
            if (owners != null && owners.Length > 0)
            {
                string? uid = owners.GetObject(0)?.GetString("UID");
                if (!string.IsNullOrEmpty(uid)) return uid;
            }
        }
        catch { }

        return "";
    }

    /// <summary>
    /// Parses a single discovery record JSON object into a display-friendly struct.
    /// </summary>
    internal static DiscoveryRecord ParseRecord(JsonObject record, string? playerNameOverride = null)
    {
        string discoveryType = "";
        string discoveredBy = "";
        string platform = "";
        string timestamp = "";
        string galaxyName = "";
        int realityIndex = -1;
        string portalHex = "";
        string customName = "";

        try
        {
            // DD = Discovery Data sub-object
            var dd = record.GetObject("DD");
            if (dd != null)
            {
                discoveryType = dd.GetString("DT") ?? "";

                // UA = Universal Address (stored as a large integer or hex string).
                // The UA is a 56-bit value packed as:
                //   [00][P][SSS][GG][YY][ZZZ][XXX]  (hex)
                // where P=planet, SSS=system, GG=galaxy, YY/ZZZ/XXX=coordinates.
                // Extract fields via bit shifts to avoid string-slicing issues.
                // UA may be a numeric value or a hex string prefixed with "0x".
                try
                {
                    long uaLong = ParseUA(dd);
                    int x   = (int)(uaLong & 0xFFF);
                    int z   = (int)((uaLong >> 12) & 0xFFF);
                    int y   = (int)((uaLong >> 24) & 0xFF);
                    int gal = (int)((uaLong >> 32) & 0xFF);
                    int sys = (int)((uaLong >> 40) & 0xFFF);
                    int pla = (int)((uaLong >> 52) & 0xF);

                    portalHex = $"{pla:X1}{sys:X3}{y:X2}{z:X3}{x:X3}";
                    realityIndex = gal;
                    galaxyName = GalaxyDatabase.GetGalaxyDisplayName(gal);
                }
                catch { /* UA may be missing or invalid */ }

                customName = dd.GetString("CN") ?? "";
            }

            // TS = Timestamp (Unix epoch seconds, stored as number or hex string)
            try
            {
                long ts = ParseLong(record, "TS");
                timestamp = FormatTimestamp(ts);
            }
            catch { /* TS may be missing */ }

            // OWS = Ownership
            var ows = record.GetObject("OWS");
            if (ows != null)
            {
                discoveredBy = ows.GetString("USN") ?? "";
                platform = ows.GetString("PTK") ?? "";
            }

            // For Available records that lack OWS data, use the player name override
            if (string.IsNullOrEmpty(discoveredBy) && !string.IsNullOrEmpty(playerNameOverride))
                discoveredBy = playerNameOverride;
        }
        catch { /* Gracefully handle malformed records */ }

        return new DiscoveryRecord(
            discoveryType, discoveredBy, platform, timestamp,
            galaxyName, realityIndex, portalHex, customName);
    }

    /// <summary>
    /// Converts a Unix epoch timestamp (seconds) to a human-readable local date/time string.
    /// </summary>
    internal static string FormatTimestamp(long unixSeconds)
    {
        if (unixSeconds <= 0) return "";
        try
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Reads the UA field from a DD sub-object, handling both numeric and hex-string formats.
    /// Numeric UAs are read via <see cref="JsonObject.GetDecimal"/> and cast to <c>long</c>.
    /// Hex-string UAs (e.g. "0x0012ABC00FF123456") are parsed from the hex digits after the "0x" prefix.
    /// </summary>
    internal static long ParseUA(JsonObject dd)
    {
        var raw = dd.GetValue("UA");

        // Try numeric first (integer or decimal stored as a number in JSON)
        if (raw is not string)
            return (long)Convert.ToDecimal(raw);

        // Hex-string format: "0x00PSSSGGYYZZZZXXX"
        string s = (string)raw;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(s[2..], 16);

        // Fallback: try parsing as a plain decimal string
        return long.Parse(s);
    }

    /// <summary>
    /// Reads a long value from a JSON object field, handling both numeric and hex-string formats.
    /// Used for fields like TS that may be stored as a number or as a "0x…" hex string.
    /// </summary>
    internal static long ParseLong(JsonObject obj, string fieldName)
    {
        var raw = obj.GetValue(fieldName);

        if (raw is not string)
            return raw is RawDouble rd ? (long)rd.Value : Convert.ToInt64(raw);

        string s = (string)raw;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(s[2..], 16);

        return long.Parse(s);
    }

    // ---- Saved Discoveries persistence ----

    /// <summary>
    /// Creates a <see cref="SavedDiscoveryEntry"/> from a parsed <see cref="DiscoveryRecord"/>
    /// plus the current save metadata.
    /// </summary>
    internal static SavedDiscoveryEntry CreateSavedEntry(
        DiscoveryRecord record, string saveName, string saveUniversalId)
    {
        return new SavedDiscoveryEntry
        {
            DiscoveryType = record.DiscoveryType,
            DiscoveredBy = record.DiscoveredBy,
            Platform = record.Platform,
            Timestamp = record.Timestamp,
            GalaxyName = record.GalaxyName,
            RealityIndex = record.RealityIndex,
            PortalHex = record.PortalHex,
            CustomName = record.CustomName,
            SaveName = saveName,
            SaveUniversalId = saveUniversalId,
            UserLabel = record.CustomName,
        };
    }

    /// <summary>
    /// Returns the full path to the saved discoveries JSON file inside the NMSE config directory.
    /// </summary>
    internal static string GetSavedDiscoveriesPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appData, "NMSE");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, SavedDiscoveriesFileName);
    }

    /// <summary>
    /// Loads the list of user-saved discovery entries from disk.
    /// Returns an empty list if the file does not exist or is invalid.
    /// </summary>
    internal static List<SavedDiscoveryEntry> LoadSavedDiscoveries()
    {
        string path = GetSavedDiscoveriesPath();
        if (!File.Exists(path)) return new List<SavedDiscoveryEntry>();

        try
        {
            string json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListSavedDiscoveryEntry);
            return list ?? new List<SavedDiscoveryEntry>();
        }
        catch
        {
            return new List<SavedDiscoveryEntry>();
        }
    }

    /// <summary>
    /// Persists the list of user-saved discovery entries to disk.
    /// </summary>
    internal static void SaveSavedDiscoveries(List<SavedDiscoveryEntry> entries)
    {
        string path = GetSavedDiscoveriesPath();
        try
        {
            string json = JsonSerializer.Serialize(entries, AppJsonContext.Default.ListSavedDiscoveryEntry);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save discoveries: {ex.Message}");
        }
    }
}
