using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NMSE.Core;
using NMSE.Data;
using NMSE.Models;
using NMSE.UI.Util;

namespace NMSE.UI.Panels;

public partial class DiscoveryPanel : UserControl
{
    /// <summary>All parsed discovery records from the save file.</summary>
    private List<DiscoveryLogic.DiscoveryRecord> _allRecords = new();

    /// <summary>
    /// Multiplier to convert pixel height to em size for the glyph font.
    /// Derived from 0.75 (points-per-pixel at 96 DPI) × 1.15 (scale factor
    /// to maximise x-height within the cell while avoiding clipping).
    /// </summary>
    private const float GlyphFontSizeMultiplier = 0.863f;

    private static readonly string AllFilterValue = "(All)";

    public DiscoveryPanel()
    {
        InitializeComponent();
        _discoveryGrid.CellPainting += OnCellPainting;
    }

    public void LoadData(JsonObject saveData)
    {
        SuspendLayout();
        try
        {
            _allRecords.Clear();
            _discoveryGrid.Rows.Clear();

            var records = DiscoveryLogic.FindDiscoveryRecords(saveData);
            if (records == null || records.Length == 0)
            {
                UpdateSummaryLabel();
                return;
            }

            // Parse all records
            for (int i = 0; i < records.Length; i++)
            {
                var rec = records.GetObject(i);
                if (rec == null) continue;
                _allRecords.Add(DiscoveryLogic.ParseRecord(rec));
            }

            PopulateFilterCombos();
            PopulateGrid(_allRecords);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    public void SaveData(JsonObject saveData)
    {
        // Read-only panel — no data to save back
    }

    public void ApplyUiLocalisation()
    {
        if (_tabControl.TabPages.Count >= 1)
            _tabControl.TabPages[0].Text = UiStrings.Get("discovery.tab_stored");
    }

    // ---- Filtering ----

    private void PopulateFilterCombos()
    {
        // Collect unique values
        var usernames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var galaxies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in _allRecords)
        {
            if (!string.IsNullOrEmpty(r.DiscoveredBy)) usernames.Add(r.DiscoveredBy);
            if (!string.IsNullOrEmpty(r.GalaxyName)) galaxies.Add(r.GalaxyName);
            if (!string.IsNullOrEmpty(r.DiscoveryType)) types.Add(r.DiscoveryType);
        }

        PopulateCombo(_usernameFilter, usernames);
        PopulateCombo(_galaxyFilter, galaxies);
        PopulateCombo(_typeFilter, types);
    }

    private static void PopulateCombo(ComboBox combo, SortedSet<string> values)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.Add(AllFilterValue);
        foreach (var v in values)
            combo.Items.Add(v);
        combo.SelectedIndex = 0;
        combo.EndUpdate();
    }

    private void ApplyFilters()
    {
        string? userFilter = _usernameFilter.SelectedItem as string;
        string? galaxyFilter = _galaxyFilter.SelectedItem as string;
        string? typeFilter = _typeFilter.SelectedItem as string;

        bool filterUser = userFilter != null && userFilter != AllFilterValue;
        bool filterGalaxy = galaxyFilter != null && galaxyFilter != AllFilterValue;
        bool filterType = typeFilter != null && typeFilter != AllFilterValue;

        var filtered = new List<DiscoveryLogic.DiscoveryRecord>();
        foreach (var r in _allRecords)
        {
            if (filterUser && !string.Equals(r.DiscoveredBy, userFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (filterGalaxy && !string.Equals(r.GalaxyName, galaxyFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (filterType && !string.Equals(r.DiscoveryType, typeFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            filtered.Add(r);
        }

        PopulateGrid(filtered);
    }

    // ---- Grid population ----

    private void PopulateGrid(List<DiscoveryLogic.DiscoveryRecord> records)
    {
        _discoveryGrid.SuspendLayout();
        try
        {
            _discoveryGrid.Rows.Clear();

            var rows = new List<DataGridViewRow>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var row = new DataGridViewRow();
                row.CreateCells(_discoveryGrid,
                    i + 1,
                    r.DiscoveryType,
                    r.DiscoveredBy,
                    r.Timestamp,
                    r.GalaxyName,
                    r.PortalHex, // stored as text, painted as glyphs
                    r.CustomName);
                rows.Add(row);
            }

            _discoveryGrid.Rows.AddRange(rows.ToArray());
            UpdateSummaryLabel();
        }
        finally
        {
            _discoveryGrid.ResumeLayout(true);
        }
    }

    private void UpdateSummaryLabel()
    {
        int shown = _discoveryGrid.Rows.Count;
        int total = _allRecords.Count;
        _summaryLabel.Text = shown == total
            ? $"{total} discoveries"
            : $"{shown} / {total} discoveries";
    }

    // ---- Glyph rendering in the Portal Glyphs column ----

    private void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        // Only custom-paint the "PortalGlyphs" column data cells
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (_discoveryGrid.Columns[e.ColumnIndex].Name != "PortalGlyphs") return;

        string portalHex = e.Value as string ?? "";

        // Paint background and selection
        e.PaintBackground(e.ClipBounds, true);

        if (!string.IsNullOrEmpty(portalHex) && e.Graphics != null)
        {
            var glyphFamily = FontManager.GlyphFont;
            if (glyphFamily != null)
            {
                DrawGlyphs(e.Graphics, portalHex, glyphFamily, e.CellBounds);
            }
            else
            {
                // Fallback: draw as plain text
                TextRenderer.DrawText(e.Graphics, portalHex, _discoveryGrid.DefaultCellStyle.Font ?? DefaultFont,
                    e.CellBounds, e.CellStyle?.ForeColor ?? SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Renders portal glyph characters using the NMS_Glyphs_Mono.ttf font via GraphicsPath
    /// for high-quality anti-aliased rendering.
    /// </summary>
    private static void DrawGlyphs(Graphics g, string portalHex, FontFamily glyphFamily, Rectangle cellBounds)
    {
        float pixelSize = cellBounds.Height - 4; // leave 2px margin top+bottom
        if (pixelSize < 6) return;

        // Convert pixel size to em size: emSize = pixelSize * multiplier * (dpiY / 72)
        float emSize = pixelSize * GlyphFontSizeMultiplier * (g.DpiY / 72f);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float x = cellBounds.X + 4;
        float y = cellBounds.Y + 2;

        using var path = new GraphicsPath();
        path.AddString(portalHex, glyphFamily, (int)FontStyle.Regular, emSize,
            new PointF(x, y), StringFormat.GenericDefault);
        g.FillPath(SystemBrushes.ControlText, path);
    }
}
