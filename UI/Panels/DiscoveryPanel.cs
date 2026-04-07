using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NMSE.Core;
using NMSE.Data;
using NMSE.Models;
using NMSE.UI.Util;

namespace NMSE.UI.Panels;

public partial class DiscoveryPanel : UserControl
{
    /// <summary>All parsed stored discovery records from the save file.</summary>
    private List<DiscoveryLogic.DiscoveryRecord> _allRecords = new();

    /// <summary>All parsed available discovery records from the save file.</summary>
    private List<DiscoveryLogic.DiscoveryRecord> _allAvailableRecords = new();

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
    }

    public void LoadData(JsonObject saveData)
    {
        SuspendLayout();
        try
        {
            // --- Stored discoveries ---
            _allRecords.Clear();
            _discoveryGrid.Rows.Clear();

            var records = DiscoveryLogic.FindDiscoveryRecords(saveData);
            if (records != null && records.Length > 0)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records.GetObject(i);
                    if (rec == null) continue;
                    _allRecords.Add(DiscoveryLogic.ParseRecord(rec));
                }
            }

            PopulateFilterCombos();
            PopulateGrid(_discoveryGrid, _allRecords, _summaryLabel, _allRecords.Count);

            // --- Available discoveries ---
            _allAvailableRecords.Clear();
            _availableGrid.Rows.Clear();

            var availRecords = DiscoveryLogic.FindAvailableRecords(saveData);
            if (availRecords != null && availRecords.Length > 0)
            {
                for (int i = 0; i < availRecords.Length; i++)
                {
                    var rec = availRecords.GetObject(i);
                    if (rec == null) continue;
                    _allAvailableRecords.Add(DiscoveryLogic.ParseRecord(rec));
                }
            }

            PopulateAvailableFilterCombos();
            PopulateGrid(_availableGrid, _allAvailableRecords, _availSummaryLabel, _allAvailableRecords.Count);
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
        if (_tabControl.TabPages.Count >= 2)
            _tabControl.TabPages[1].Text = UiStrings.Get("discovery.tab_available");
    }

    // ---- Stored Filtering ----

    private void PopulateFilterCombos()
    {
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

    private void ApplyFilters()
    {
        string? userFilter = _usernameFilter.SelectedItem as string;
        string? galaxyFilter = _galaxyFilter.SelectedItem as string;
        string? typeFilter = _typeFilter.SelectedItem as string;

        var filtered = FilterRecords(_allRecords, userFilter, galaxyFilter, typeFilter);
        PopulateGrid(_discoveryGrid, filtered, _summaryLabel, _allRecords.Count);
    }

    // ---- Available Filtering ----

    private void PopulateAvailableFilterCombos()
    {
        var usernames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var galaxies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in _allAvailableRecords)
        {
            if (!string.IsNullOrEmpty(r.DiscoveredBy)) usernames.Add(r.DiscoveredBy);
            if (!string.IsNullOrEmpty(r.GalaxyName)) galaxies.Add(r.GalaxyName);
            if (!string.IsNullOrEmpty(r.DiscoveryType)) types.Add(r.DiscoveryType);
        }

        PopulateCombo(_availUsernameFilter, usernames);
        PopulateCombo(_availGalaxyFilter, galaxies);
        PopulateCombo(_availTypeFilter, types);
    }

    private void ApplyAvailableFilters()
    {
        string? userFilter = _availUsernameFilter.SelectedItem as string;
        string? galaxyFilter = _availGalaxyFilter.SelectedItem as string;
        string? typeFilter = _availTypeFilter.SelectedItem as string;

        var filtered = FilterRecords(_allAvailableRecords, userFilter, galaxyFilter, typeFilter);
        PopulateGrid(_availableGrid, filtered, _availSummaryLabel, _allAvailableRecords.Count);
    }

    // ---- Shared helpers ----

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

    private static List<DiscoveryLogic.DiscoveryRecord> FilterRecords(
        List<DiscoveryLogic.DiscoveryRecord> source,
        string? userFilter, string? galaxyFilter, string? typeFilter)
    {
        bool filterUser = userFilter != null && userFilter != AllFilterValue;
        bool filterGalaxy = galaxyFilter != null && galaxyFilter != AllFilterValue;
        bool filterType = typeFilter != null && typeFilter != AllFilterValue;

        var filtered = new List<DiscoveryLogic.DiscoveryRecord>();
        foreach (var r in source)
        {
            if (filterUser && !string.Equals(r.DiscoveredBy, userFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (filterGalaxy && !string.Equals(r.GalaxyName, galaxyFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (filterType && !string.Equals(r.DiscoveryType, typeFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            filtered.Add(r);
        }

        return filtered;
    }

    // ---- Grid population ----

    private static void PopulateGrid(DataGridView grid, List<DiscoveryLogic.DiscoveryRecord> records,
        Label summaryLabel, int totalCount)
    {
        grid.SuspendLayout();
        try
        {
            grid.Rows.Clear();

            var rows = new List<DataGridViewRow>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var row = new DataGridViewRow();
                row.CreateCells(grid,
                    i + 1,
                    r.DiscoveryType,
                    r.DiscoveredBy,
                    r.Timestamp,
                    r.GalaxyName,
                    r.PortalHex,   // Portal Glyphs — painted via OnCellPainting
                    r.PortalHex,   // Portal Code — plain text
                    r.CustomName);
                // Store reality index in Galaxy cell's Tag for colored-dot painting
                row.Cells[4].Tag = r.RealityIndex;
                rows.Add(row);
            }

            grid.Rows.AddRange(rows.ToArray());
            UpdateSummaryLabel(summaryLabel, grid.Rows.Count, totalCount);
        }
        finally
        {
            grid.ResumeLayout(true);
        }
    }

    private static void UpdateSummaryLabel(Label label, int shown, int total)
    {
        label.Text = shown == total
            ? $"{total} discoveries"
            : $"{shown} / {total} discoveries";
    }

    // ---- Cell painting: Portal Glyphs + Galaxy colored dot ----

    private void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var grid = sender as DataGridView;
        if (grid == null) return;

        string colName = grid.Columns[e.ColumnIndex].Name;

        if (colName == "PortalGlyphs")
        {
            PaintPortalGlyphs(grid, e);
        }
        else if (colName == "Galaxy")
        {
            PaintGalaxyWithDot(grid, e);
        }
    }

    /// <summary>
    /// Renders portal glyph characters using the NMS_Glyphs_Mono font via GraphicsPath
    /// for high-quality anti-aliased rendering.
    /// </summary>
    private static void PaintPortalGlyphs(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        string portalHex = e.Value as string ?? "";

        e.PaintBackground(e.ClipBounds, e.State.HasFlag(DataGridViewElementStates.Selected));

        if (!string.IsNullOrEmpty(portalHex) && e.Graphics != null)
        {
            var glyphFamily = FontManager.GlyphFont;
            if (glyphFamily != null)
            {
                float pixelSize = e.CellBounds.Height - 4;
                if (pixelSize >= 6)
                {
                    float emSize = pixelSize * GlyphFontSizeMultiplier * (e.Graphics.DpiY / 72f);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    float x = e.CellBounds.X + 4;
                    float y = e.CellBounds.Y + 2;

                    using var path = new GraphicsPath();
                    path.AddString(portalHex, glyphFamily, (int)FontStyle.Regular, emSize,
                        new PointF(x, y), StringFormat.GenericDefault);

                    var brush = e.State.HasFlag(DataGridViewElementStates.Selected)
                        ? new SolidBrush(e.CellStyle?.SelectionForeColor ?? SystemColors.HighlightText)
                        : SystemBrushes.ControlText;
                    try
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                    finally
                    {
                        // Only dispose if we created a new brush (selected state)
                        if (e.State.HasFlag(DataGridViewElementStates.Selected) && brush is SolidBrush sb)
                            sb.Dispose();
                    }
                }
            }
            else
            {
                // Fallback: draw as plain text
                TextRenderer.DrawText(e.Graphics, portalHex,
                    grid.DefaultCellStyle.Font ?? grid.Font,
                    e.CellBounds,
                    e.CellStyle?.ForeColor ?? SystemColors.ControlText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Paints the Galaxy column with the galaxy name plus a colored ● indicator
    /// representing the galaxy type (Normal=blue, Lush=green, Harsh=red, Empty=cyan).
    /// Same rendering as teleport destinations in CataloguePanel.
    /// </summary>
    private static void PaintGalaxyWithDot(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        e.PaintBackground(e.ClipBounds, e.State.HasFlag(DataGridViewElementStates.Selected));

        string text = e.Value?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text) && e.Graphics != null)
        {
            int realityIndex = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is int ri ? ri : 0;
            string galaxyType = GalaxyDatabase.GetGalaxyType(realityIndex);
            Color dotColor = GalaxyDatabase.GetGalaxyTypeColor(galaxyType);

            var font = e.CellStyle?.Font ?? grid.DefaultCellStyle.Font ?? grid.Font;
            var textColor = e.State.HasFlag(DataGridViewElementStates.Selected)
                ? (e.CellStyle?.SelectionForeColor ?? SystemColors.HighlightText)
                : (e.CellStyle?.ForeColor ?? SystemColors.ControlText);

            using var textBrush = new SolidBrush(textColor);
            using var dotBrush = new SolidBrush(dotColor);

            var textSize = e.Graphics.MeasureString(text + " ", font);
            var rect = e.CellBounds;
            rect.X += 2;
            e.Graphics.DrawString(text + " ", font, textBrush, rect.X, rect.Y + (rect.Height - textSize.Height) / 2);
            e.Graphics.DrawString("\u25CF", font, dotBrush, rect.X + textSize.Width - 2, rect.Y + (rect.Height - textSize.Height) / 2);
        }

        e.Handled = true;
    }
}
