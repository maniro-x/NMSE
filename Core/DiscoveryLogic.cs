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

                // UA = Universal Address (stored as a large integer)
                try
                {
                    decimal ua = dd.GetDecimal("UA");
                    string uaHex = CoordinateHelper.UAIntegertoUAHex(ua);
                    if (!string.IsNullOrEmpty(uaHex))
                    {
                        var (isSuccess, portal, galaxy) = CoordinateHelper.UAHextoPortalHexPlusRealityIndex(uaHex);
                        if (isSuccess)
                        {
                            portalHex = portal;
                            if (int.TryParse(galaxy, System.Globalization.NumberStyles.HexNumber, null, out int ri))
                            {
                                realityIndex = ri;
                                galaxyName = GalaxyDatabase.GetGalaxyDisplayName(ri);
                            }
                        }
                    }
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
}
