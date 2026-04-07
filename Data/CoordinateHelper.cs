using System;
using System.IO;
#if WINFORMS
using System.Drawing;
using System.Windows.Forms;
#endif

namespace NMSE.Data;

/// <summary>
/// NMS galactic coordinate conversion utilities.
/// NMS uses many different formats (in-game or in the save file)
/// to specify galactic coordinates.
/// (P = Planet Index / S = Star System Index / Y = Height / Z = Width / X = Length)
/// 1. Portal Glyphs: [P][SSS][YY][ZZZ][XXX]
/// 12 character hex (0-F), 12 set of numbers (1-16) or graphical representation
/// 2. Signal Booster: (AAAA:XXXX:YYYY:ZZZZ:SSSS)
/// AAAA is 4 random letters; Does not include the planet index
/// 3. Voxel Coordinates: 
/// 4. UnivseralAddress: Int64 or hex
/// Functions are provided to convert any type to any other type
/// </summary>
public static class CoordinateHelper
{
    /// <summary>Validate string is hex characters only.</summary>
    private static bool IsHexString(string s)
    {
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(2);
        }
          
        foreach (char c in s)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Verify the portal code is valid. This includes validating
    /// the portal is in hex, 12 characters, and does not produce an
    /// invalid address as reported by the in game portal mechanic.
    /// (Or replaced with a rounded value.)
    /// TODO: Add Unit tests for invalid portalcodes
    /// </summary>
    private static bool IsValidPortal(string portalCode)
    {
        if (string.IsNullOrEmpty(portalCode) || portalCode.Length != 12 || (!IsHexString(portalCode)))
        return false;

        int planetIndex = Convert.ToInt32(portalCode[..1], 16);
        int systemIndex = Convert.ToInt32(portalCode[1..4], 16);
        int rawY = Convert.ToInt32(portalCode[4..6], 16);
        int rawZ = Convert.ToInt32(portalCode[6..9], 16);
        int rawX = Convert.ToInt32(portalCode[9..12], 16);
        /// P-Value Check: 7-16 is invalid, systems can only have
        /// up to 6 planets Allow 0 for the system even though it
        /// isn't valid planet
        if (planetIndex >= 7)
        return false;
        /// SSI-Value Check: 000 is invalid, 0x300-0x3E7 (768-999) is invalid
        /// 0x243 (579) is highest found so 0x244-0x2FF (580-767) is likely invalid
        /// Purple systems start at 0x3E9 (1001), above is before Worlds2
        if (systemIndex == 0 || systemIndex is >=580 and <=999)
            return false;
        /// Y-Value check: 80 (128) is invalid (80->81)
        if (rawY == 128)
            return false;
        /// X-Value check: 800 (2048) is invalid (800->801)
        if (rawX == 2048)
            return false;
        /// Z-Value check: 800 (2048) is invalid (800->801)
        if (rawZ == 2048)
            return false;
        /// Less than 3000 LY from galactic core is invalid
        /// 100104005005 is the reliable galactic core address
        /// Below this is likely invalid (needs exact verification)
        if (rawY < 64 && rawZ < 5 && rawX < 5)
            return false;
        else
            return true;
    }
    /// TODO: Special indentication of Purple, Atlas, Blackhole and Glass systems.
    /// Special SolarSystemIndex: 079 = Blackhole, 07A = Atlas, 3E8 = Glass, 
    /// Purple systems are SSI starting at 1001/3E9+

    /// <summary>
    /// Convert a hex portal code string to a decimal string where each hex digit
    /// is converted to its decimal value + 1 (0->1, 1->2, ..., F->16), comma-separated.
    /// Example: "00E4FF91310A" -> "1,1,15,5,16,16,10,2,4,2,1,11"
    /// </summary>
    public static string PortalHexToDec(string portalCode)
    {
        if (!IsValidPortal(portalCode)) return "";
        var parts = new List<string>(portalCode.Length);
        foreach (char hexChar in portalCode)
        {
            int val = System.Uri.FromHex(hexChar);
            parts.Add((val + 1).ToString());
        }
        return string.Join(",", parts);
    }

    /// TODO: PortalDextoHex conversion

    /// <summary>
    /// Parse a 12-character portal code (hex) back into voxel coordinates.
    /// Format: {planetIndex:1}{systemIndex:3}{y:2}{z:3}{x:3}
    /// Returns true if parsing was successful.
    /// </summary>
    public static bool PortalCodeToVoxel(string portalCode, out int voxelX, out int voxelY, out int voxelZ, out int systemIndex, out int planetIndex)
    {
        voxelX = voxelY = voxelZ = systemIndex = planetIndex = 0;

        if (!IsValidPortal(portalCode))
            return false;

        planetIndex = Convert.ToInt32(portalCode[..1], 16);
        systemIndex = Convert.ToInt32(portalCode[1..4], 16);
        int rawY = Convert.ToInt32(portalCode[4..6], 16);
        int rawZ = Convert.ToInt32(portalCode[6..9], 16);
        int rawX = Convert.ToInt32(portalCode[9..12], 16);

        voxelX = ConvertAddressToVoxel(rawX, 3);
        voxelY = ConvertAddressToVoxel(rawY, 2);
        voxelZ = ConvertAddressToVoxel(rawZ, 3);
        return true;
    }

    /// TODO: Portal to UAHex -- needs RealityIndex/Galaxy

    /// <summary>Convert voxel coordinates to a 12-character portal code.</summary>
    public static string VoxelToPortalCode(int voxelX, int voxelY, int voxelZ, int systemIndex, int planetIndex)
    {
        int x = ConvertVoxelForAddress(voxelX, 3);
        int y = ConvertVoxelForAddress(voxelY, 2);
        int z = ConvertVoxelForAddress(voxelZ, 3);
        return $"{planetIndex:X1}{systemIndex:X3}{y:X2}{z:X3}{x:X3}";
    }

    /// <summary>Convert voxel coordinates to signal booster format (XXXX:YYYY:ZZZZ:SSSS).</summary>
    public static string VoxelToSignalBooster(int voxelX, int voxelY, int voxelZ, int systemIndex)
    {
        int x = voxelX + GetShiftValue(3);
        int y = voxelY + GetShiftValue(2);
        int z = voxelZ + GetShiftValue(3);
        return $"{x:X4}:{y:X4}:{z:X4}:{systemIndex:X4}";
    }

    private static int ConvertVoxelForAddress(int value, int byteLength)
    {
        int signValue = (int)Math.Pow(16, byteLength);
        int num = value % signValue;
        return num >= 0 ? num : num + signValue;
    }

    private static int GetShiftValue(int byteLength)
    {
        return (int)(0.5 * Math.Pow(16, byteLength) - 1);
    }

    /// <summary>Reverse of ConvertVoxelForAddress: address value back to signed voxel.</summary>
    private static int ConvertAddressToVoxel(int address, int byteLength)
    {
        int signValue = (int)Math.Pow(16, byteLength);
        int halfSign = signValue / 2;
        return address >= halfSign ? address - signValue : address;
    }
    
    /// <summary>
    /// Converss UA interger values into a UA hex value
    /// UAs as a 64 bit interger is found frequently
    /// in the save file. It is a simple conversion
    /// from the same value into hex but prefixed
    /// with 0x and padded with two 0s (actually 56bit)
    /// Possible values are 0-72,057,594,037,927,935 or (2^56)-1
    /// UAs in hex are usually prefixed with 0x so
    /// it will be output as the same.
    /// </summary>
    public static string UAIntegertoUAHex(decimal UAInt)
    {
        decimal maxInt = ((decimal)Math.Pow((double)2, (double)56) -1);
        if ((!decimal.IsInteger(UAInt)) || decimal.IsNegative(UAInt) || UAInt > maxInt)
            return "";
        string hexValue = UAInt.ToString("X16");
        string UAHex = "0x" + hexValue;
        return UAHex;
    }

    /// <summary>
    /// Converts UAs in hex to portal code + reality index
    /// UAs in hex are essentially 0-padded portal codes with
    /// the galaxy ids stuck in the middle.
    /// [00][P][SSS][GG][YY][ZZZ][XXX]
    /// Outputs the RealityIndex and PortalCode
    /// </summary>
    public static (bool IsSuccess, string portalHex, string galaxyHex) UAHextoPortalHexPlusRealityIndex(string UAHex)
    {
        string planetHex, systemHex, galaxyHex, yHex, zHex, xHex, portalHex;
        if ((!IsHexString(UAHex)) || UAHex.Length != 18)
        return (false, "", "");
        // First two string characters are expexted to be 0x
        planetHex = UAHex[..5];
        systemHex = UAHex[6..8];
        galaxyHex = UAHex[9..10];
        yHex = UAHex[11..12];
        zHex = UAHex[13..15];
        xHex = UAHex[16..18];
        portalHex = planetHex + systemHex + yHex + zHex + xHex;
        if (!IsValidPortal(portalHex))
        return (false, "", "");
        else
        return (true, portalHex, galaxyHex);
    }

    /// <summary>
    /// Convert Up Values from placed objects to planetary
    /// coordinates to two decimal places as a single string
    /// </summary>
    public static string ConvertUpToPlanetCoords(double UpZ, double UpY, double UpX)
    {
        double latitude = (Math.Asin(UpZ) * (180 / Math.PI));
        double longitude = (Math.Atan2(UpY, UpX) * (180 / Math.PI));
        string strLat = string.Format("{0:F2}", latitude);
        string strLong = string.Format("{0:F2}", longitude);
        string planetCoords =  strLat + ", " + strLong;
        return planetCoords;
    }

    /// <summary>Compute straight-line distance to galaxy center in light-years.</summary>
    public static double GetDistanceToCenter(int voxelX, int voxelY, int voxelZ)
    {
        return Math.Sqrt(voxelX * voxelX + voxelY * voxelY + voxelZ * voxelZ) * 100.0;
    }

    /// <summary>Compute approximate number of jumps to reach the center at the given ly per jump.</summary>
    public static int GetJumpsToCenter(double distanceToCenter, double distancePerJump)
    {
        if (distancePerJump <= 0) return 0;
        return (int)Math.Ceiling(distanceToCenter / distancePerJump);
    }

    /// <summary>Default hyperdrive range used when calculation isn't available.</summary>
    public const double DefaultHyperdriveRange = 100.0;

    /// <summary>Seconds between space battles (game mechanic: 3 hours real-time).</summary>
    public const int SpaceBattleIntervalSeconds = 10800;

    /// <summary>Warps between space battles (game mechanic: every 5 warps).</summary>
    public const int SpaceBattleIntervalWarps = 5;

    /// <summary>Player state values.</summary>
    public static readonly string[] PlayerStates =
    {
        "OnFoot", "InShip", "InStation", "AboardFleet", "InNexus",
        "AbandonedFreighter", "InShipLanded", "InVehicle",
        "OnFootInCorvette", "OnFootInCorvetteLanded"
    };

    /// <summary>Localisation keys corresponding to PlayerStates, for display in combo boxes.</summary>
    public static readonly string[] PlayerStateLocKeys =
    {
        "player.state_on_foot", "player.state_in_ship", "player.state_in_station",
        "player.state_aboard_fleet", "player.state_in_nexus", "player.state_abandoned_freighter",
        "player.state_in_ship_landed", "player.state_in_vehicle",
        "player.state_on_foot_corvette", "player.state_on_foot_corvette_landed"
    };

#if WINFORMS
    /// <summary>Glyph image cache, indexed by hex digit 0-F.</summary>
    private static readonly Dictionary<char, Image?> _glyphCache = new();
    private static string? _glyphBasePath;

    /// <summary>Set the base path where glyph images (UI-GLYPH1.PNG etc.) are located.</summary>
    public static void SetGlyphBasePath(string basePath)
    {
        _glyphBasePath = basePath;
        _glyphCache.Clear();
    }

    /// <summary>Get the glyph image for a hex digit (0-9, A-F). Returns null if not found.</summary>
    public static Image? GetGlyphImage(char hexDigit)
    {
        hexDigit = char.ToUpperInvariant(hexDigit);
        if (_glyphCache.TryGetValue(hexDigit, out var cached))
            return cached;

        Image? img = LoadGlyphImage(hexDigit);
        _glyphCache[hexDigit] = img;
        return img;
    }

    private static Image? LoadGlyphImage(char hexDigit)
    {
        if (string.IsNullOrEmpty(_glyphBasePath)) return null;

        // Glyph files are numbered 1-16 mapping to hex digits 0-F
        int index = hexDigit >= '0' && hexDigit <= '9'
            ? hexDigit - '0' + 1
            : hexDigit >= 'A' && hexDigit <= 'F'
                ? hexDigit - 'A' + 11
                : -1;
        if (index < 1) return null;

        string path = Path.Combine(_glyphBasePath, $"UI-GLYPH{index}.PNG");
        if (!File.Exists(path)) return null;

        try { return Image.FromFile(path); }
        catch { return null; }
    }

    /// <summary>Create a FlowLayoutPanel that renders portal glyphs for a 12-character portal code.</summary>
    public static FlowLayoutPanel CreateGlyphPanel(string portalCode, int glyphSize = 22)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        if (string.IsNullOrEmpty(portalCode)) return panel;

        foreach (char c in portalCode)
        {
            var img = GetGlyphImage(c);
            if (img != null)
            {
                var glyphPanel = new Panel
                {
                    Width = glyphSize,
                    Height = glyphSize,
                    Margin = new Padding(0),
                };
                glyphPanel.Paint += (s, e) =>
                {
                    using var brush = new SolidBrush(Color.FromArgb(60, 60, 60));
                    e.Graphics.FillRectangle(brush, 0, 0, glyphPanel.Width, glyphPanel.Height);
                    e.Graphics.DrawImage(img, 0, 0, glyphPanel.Width, glyphPanel.Height);
                };
                panel.Controls.Add(glyphPanel);
            }
            else
            {
                // Fallback: show the hex character as text
                var lbl = new Label
                {
                    Text = c.ToString(),
                    Font = new Font("Consolas", 10, FontStyle.Bold),
                    Width = glyphSize,
                    Height = glyphSize,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0),
                };
                panel.Controls.Add(lbl);
            }
        }

        return panel;
    }

    /// <summary>Update the glyph images in an existing glyph panel for a new portal code.</summary>
    public static void UpdateGlyphPanel(FlowLayoutPanel panel, string portalCode, int glyphSize = 22)
    {
        panel.SuspendLayout();
        panel.Controls.Clear();

        if (!string.IsNullOrEmpty(portalCode))
        {
            foreach (char c in portalCode)
            {
                var img = GetGlyphImage(c);
                if (img != null)
                {
                    var glyphPanel = new Panel
                    {
                        Width = glyphSize,
                        Height = glyphSize,
                        Margin = new Padding(0),
                    };
                    glyphPanel.Paint += (s, e) =>
                    {
                        using var brush = new SolidBrush(Color.FromArgb(60, 60, 60));
                        e.Graphics.FillRectangle(brush, 0, 0, glyphPanel.Width, glyphPanel.Height);
                        e.Graphics.DrawImage(img, 0, 0, glyphPanel.Width, glyphPanel.Height);
                    };
                    panel.Controls.Add(glyphPanel);
                }
                else
                {
                    var lbl = new Label
                    {
                        Text = c.ToString(),
                        Font = new Font("Consolas", 10, FontStyle.Bold),
                        Width = glyphSize,
                        Height = glyphSize,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Margin = new Padding(0),
                    };
                    panel.Controls.Add(lbl);
                }
            }
        }

        panel.ResumeLayout(true);
    }
#endif
}
