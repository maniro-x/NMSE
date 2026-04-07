using NMSE.Data;
using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Handles parsing of DiscoveryManagerData records from the save file.
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
    /// Parses a single discovery record JSON object into a display-friendly struct.
    /// </summary>
    internal static DiscoveryRecord ParseRecord(JsonObject record)
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

            // TS = Timestamp (Unix epoch seconds)
            try
            {
                long ts = record.GetLong("TS");
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
}
