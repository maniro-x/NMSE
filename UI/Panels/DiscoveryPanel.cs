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

    /// <summary>All user-saved discovery entries loaded from / persisted to JSON.</summary>
    private List<DiscoveryLogic.SavedDiscoveryEntry> _savedEntries = new();

    /// <summary>Current save name for copy-to-saved operations.</summary>
    private string _currentSaveName = "";

    /// <summary>Current save universal ID for copy-to-saved operations.</summary>
    private string _currentSaveUniversalId = "";

    /// <summary>Player name from the save file, used to pin at top of Username filters.</summary>
    private string _playerName = "";

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
            // Extract save metadata for copy-to-saved operations
            _currentSaveName = DiscoveryLogic.GetSaveName(saveData);
            _currentSaveUniversalId = DiscoveryLogic.GetSaveUniversalId(saveData);

            // Resolve player name for Available records that lack OWS data
            string playerName = DiscoveryLogic.GetPlayerName(saveData);
            _playerName = playerName;

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
                    _allAvailableRecords.Add(DiscoveryLogic.ParseRecord(rec, playerName));
                }
            }

            PopulateAvailableFilterCombos();
            PopulateGrid(_availableGrid, _allAvailableRecords, _availSummaryLabel, _allAvailableRecords.Count);

            // --- Saved discoveries (from JSON file) ---
            LoadSavedDiscoveriesTab();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    public void SaveData(JsonObject saveData)
    {
        // Read-only panel — no data to save back to the NMS save file.
        // Saved discoveries are persisted to their own JSON file via the Save button.
    }

    public void ApplyUiLocalisation()
    {
        if (_tabControl.TabPages.Count >= 1)
            _tabControl.TabPages[0].Text = UiStrings.Get("discovery.tab_stored");
        if (_tabControl.TabPages.Count >= 2)
            _tabControl.TabPages[1].Text = UiStrings.Get("discovery.tab_available");
        if (_tabControl.TabPages.Count >= 3)
            _tabControl.TabPages[2].Text = UiStrings.Get("discovery.tab_saved");
    }

    // ---- Stored Filtering ----

    private void PopulateFilterCombos()
    {
        var usernames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var galaxyIndices = new SortedSet<int>();
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in _allRecords)
        {
            if (!string.IsNullOrEmpty(r.DiscoveredBy)) usernames.Add(r.DiscoveredBy);
            if (r.RealityIndex >= 0) galaxyIndices.Add(r.RealityIndex);
            if (!string.IsNullOrEmpty(r.DiscoveryType)) types.Add(r.DiscoveryType);
        }

        PopulateUsernameCombo(_usernameFilter, usernames, _playerName);
        PopulateGalaxyCombo(_galaxyFilter, galaxyIndices);
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
        var galaxyIndices = new SortedSet<int>();
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in _allAvailableRecords)
        {
            if (!string.IsNullOrEmpty(r.DiscoveredBy)) usernames.Add(r.DiscoveredBy);
            if (r.RealityIndex >= 0) galaxyIndices.Add(r.RealityIndex);
            if (!string.IsNullOrEmpty(r.DiscoveryType)) types.Add(r.DiscoveryType);
        }

        PopulateUsernameCombo(_availUsernameFilter, usernames, _playerName);
        PopulateGalaxyCombo(_availGalaxyFilter, galaxyIndices);
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

    // ---- Saved Filtering ----

    private void PopulateSavedFilterCombos()
    {
        var usernames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var galaxyIndices = new SortedSet<int>();
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in _savedEntries)
        {
            if (!string.IsNullOrEmpty(e.DiscoveredBy)) usernames.Add(e.DiscoveredBy);
            if (e.RealityIndex >= 0) galaxyIndices.Add(e.RealityIndex);
            if (!string.IsNullOrEmpty(e.DiscoveryType)) types.Add(e.DiscoveryType);
        }

        PopulateUsernameCombo(_savedUsernameFilter, usernames, _playerName);
        PopulateGalaxyCombo(_savedGalaxyFilter, galaxyIndices);
        PopulateCombo(_savedTypeFilter, types);
    }

    private void ApplySavedFilters()
    {
        string? userFilter = _savedUsernameFilter.SelectedItem as string;
        string? galaxyFilter = _savedGalaxyFilter.SelectedItem as string;
        string? typeFilter = _savedTypeFilter.SelectedItem as string;

        PopulateSavedGrid(userFilter, galaxyFilter, typeFilter);
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

    /// <summary>
    /// Populates a username filter combo with the player name pinned as the first option
    /// after "(All)", followed by remaining usernames in alphabetical order.
    /// </summary>
    private static void PopulateUsernameCombo(ComboBox combo, SortedSet<string> usernames, string playerName)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.Add(AllFilterValue);

        // Pin the save file player name at the top if present
        if (!string.IsNullOrEmpty(playerName) && usernames.Contains(playerName))
        {
            combo.Items.Add(playerName);
        }

        foreach (var v in usernames)
        {
            if (!string.Equals(v, playerName, StringComparison.OrdinalIgnoreCase))
                combo.Items.Add(v);
        }

        combo.SelectedIndex = 0;
        combo.EndUpdate();
    }

    /// <summary>
    /// Populates a galaxy filter combo in numerical (reality-index) order
    /// instead of alphabetical name order.
    /// </summary>
    private static void PopulateGalaxyCombo(ComboBox combo, SortedSet<int> galaxyIndices)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.Add(AllFilterValue);

        foreach (int idx in galaxyIndices)
        {
            string displayName = GalaxyDatabase.GetGalaxyDisplayName(idx);
            combo.Items.Add(displayName);
        }

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

    // ---- Grid population (Stored / Available) ----

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

    // ---- Saved Discoveries tab ----

    private void LoadSavedDiscoveriesTab()
    {
        _savedEntries = DiscoveryLogic.LoadSavedDiscoveries();
        PopulateSavedFilterCombos();
        PopulateSavedGrid(null, null, null);
    }

    private void PopulateSavedGrid(string? userFilter, string? galaxyFilter, string? typeFilter)
    {
        bool filterUser = userFilter != null && userFilter != AllFilterValue;
        bool filterGalaxy = galaxyFilter != null && galaxyFilter != AllFilterValue;
        bool filterType = typeFilter != null && typeFilter != AllFilterValue;

        _savedGrid.SuspendLayout();
        try
        {
            _savedGrid.Rows.Clear();

            var rows = new List<DataGridViewRow>();
            int displayIndex = 0;
            for (int i = 0; i < _savedEntries.Count; i++)
            {
                var e = _savedEntries[i];

                // Apply filters
                if (filterUser && !string.Equals(e.DiscoveredBy, userFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filterGalaxy && !string.Equals(e.GalaxyName, galaxyFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filterType && !string.Equals(e.DiscoveryType, typeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                displayIndex++;
                var row = new DataGridViewRow();
                row.CreateCells(_savedGrid,
                    displayIndex,
                    e.UserLabel,
                    e.DiscoveryType,
                    e.DiscoveredBy,
                    e.Timestamp,
                    e.GalaxyName,
                    e.PortalHex,   // Portal Glyphs — painted via OnCellPainting
                    e.PortalHex,   // Portal Code — plain text
                    e.CustomName,
                    e.SaveName,
                    "Delete");
                // Store reality index in Galaxy cell's Tag for colored-dot painting
                row.Cells[5].Tag = e.RealityIndex;
                // Store the actual index into _savedEntries for delete operations
                row.Tag = i;
                rows.Add(row);
            }

            _savedGrid.Rows.AddRange(rows.ToArray());
            _savedSummaryLabel.Text = rows.Count == _savedEntries.Count
                ? $"{_savedEntries.Count} saved discoveries"
                : $"{rows.Count} / {_savedEntries.Count} saved discoveries";
        }
        finally
        {
            _savedGrid.ResumeLayout(true);
        }
    }

    private void CopySelectedToSaved(DataGridView sourceGrid, List<DiscoveryLogic.DiscoveryRecord> sourceRecords)
    {
        if (sourceGrid.SelectedRows.Count == 0) return;

        int rowIndex = sourceGrid.SelectedRows[0].Index;
        if (rowIndex < 0 || rowIndex >= sourceRecords.Count) return;

        var record = sourceRecords[rowIndex];
        var entry = DiscoveryLogic.CreateSavedEntry(record, _currentSaveName, _currentSaveUniversalId);
        _savedEntries.Add(entry);
        PopulateSavedFilterCombos();
        PopulateSavedGrid(null, null, null);
    }

    private void OnCopyStoredToSaved(object? sender, EventArgs e)
    {
        // When filtered, get the actual record from the displayed subset
        var displayedRecords = GetDisplayedStoredRecords();
        CopySelectedToSaved(_discoveryGrid, displayedRecords);
    }

    private void OnCopyAvailableToSaved(object? sender, EventArgs e)
    {
        var displayedRecords = GetDisplayedAvailableRecords();
        CopySelectedToSaved(_availableGrid, displayedRecords);
    }

    private List<DiscoveryLogic.DiscoveryRecord> GetDisplayedStoredRecords()
    {
        string? userFilter = _usernameFilter.SelectedItem as string;
        string? galaxyFilter = _galaxyFilter.SelectedItem as string;
        string? typeFilter = _typeFilter.SelectedItem as string;

        bool anyFilter = (userFilter != null && userFilter != AllFilterValue)
                      || (galaxyFilter != null && galaxyFilter != AllFilterValue)
                      || (typeFilter != null && typeFilter != AllFilterValue);

        return anyFilter ? FilterRecords(_allRecords, userFilter, galaxyFilter, typeFilter) : _allRecords;
    }

    private List<DiscoveryLogic.DiscoveryRecord> GetDisplayedAvailableRecords()
    {
        string? userFilter = _availUsernameFilter.SelectedItem as string;
        string? galaxyFilter = _availGalaxyFilter.SelectedItem as string;
        string? typeFilter = _availTypeFilter.SelectedItem as string;

        bool anyFilter = (userFilter != null && userFilter != AllFilterValue)
                      || (galaxyFilter != null && galaxyFilter != AllFilterValue)
                      || (typeFilter != null && typeFilter != AllFilterValue);

        return anyFilter ? FilterRecords(_allAvailableRecords, userFilter, galaxyFilter, typeFilter) : _allAvailableRecords;
    }

    private void OnSavedGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        // Check if the Delete button column was clicked
        if (e.ColumnIndex >= 0 && _savedGrid.Columns[e.ColumnIndex].Name == "DeleteBtn")
        {
            // Use the row.Tag which stores the actual index into _savedEntries
            var row = _savedGrid.Rows[e.RowIndex];
            if (row.Tag is int entryIndex && entryIndex >= 0 && entryIndex < _savedEntries.Count)
            {
                _savedEntries.RemoveAt(entryIndex);
                PopulateSavedFilterCombos();
                ApplySavedFilters();
            }
        }
    }

    private void OnSavedGridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        // Only the UserLabel column is editable
        if (e.ColumnIndex >= 0 && _savedGrid.Columns[e.ColumnIndex].Name == "UserLabel")
        {
            var row = _savedGrid.Rows[e.RowIndex];
            if (row.Tag is int entryIndex && entryIndex >= 0 && entryIndex < _savedEntries.Count)
            {
                string newLabel = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";
                _savedEntries[entryIndex].UserLabel = newLabel;
            }
        }
    }

    private void OnSaveDiscoveries(object? sender, EventArgs e)
    {
        DiscoveryLogic.SaveSavedDiscoveries(_savedEntries);
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
