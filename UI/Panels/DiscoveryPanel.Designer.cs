namespace NMSE.UI.Panels;

partial class DiscoveryPanel
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        DoubleBuffered = true;
        SuspendLayout();

        _tabControl = new DoubleBufferedTabControl { Dock = DockStyle.Fill };

        // === Tab 1: Stored Discoveries ===
        var tab1 = new TabPage("Stored Discoveries");
        var container = new Panel { Dock = DockStyle.Fill };

        // --- Filter bar ---
        _filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4),
        };

        _filterPanel.Controls.Add(new Label
        {
            Text = "Username:",
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0),
        });
        _usernameFilter = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 12, 0),
        };
        _usernameFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
        _filterPanel.Controls.Add(_usernameFilter);

        _filterPanel.Controls.Add(new Label
        {
            Text = "Galaxy:",
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0),
        });
        _galaxyFilter = new ComboBox
        {
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 12, 0),
        };
        _galaxyFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
        _filterPanel.Controls.Add(_galaxyFilter);

        _filterPanel.Controls.Add(new Label
        {
            Text = "Type:",
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0),
        });
        _typeFilter = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 12, 0),
        };
        _typeFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
        _filterPanel.Controls.Add(_typeFilter);

        _summaryLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(8, 5, 0, 0),
        };
        _filterPanel.Controls.Add(_summaryLabel);

        container.Controls.Add(_filterPanel);

        // --- DataGridView ---
        _discoveryGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        };

        _discoveryGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", Width = 55, SortMode = DataGridViewColumnSortMode.Automatic },
            new DataGridViewTextBoxColumn { Name = "DiscoveryType", HeaderText = "Discovery Type", Width = 120, SortMode = DataGridViewColumnSortMode.Automatic },
            new DataGridViewTextBoxColumn { Name = "DiscoveredBy", HeaderText = "Discovered By", Width = 140, SortMode = DataGridViewColumnSortMode.Automatic },
            new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Timestamp", Width = 150, SortMode = DataGridViewColumnSortMode.Automatic },
            new DataGridViewTextBoxColumn { Name = "Galaxy", HeaderText = "Galaxy", Width = 150, SortMode = DataGridViewColumnSortMode.Automatic },
            new DataGridViewTextBoxColumn { Name = "PortalGlyphs", HeaderText = "Portal Glyphs", Width = 200, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "CustomName", HeaderText = "Custom Name", Width = 150, SortMode = DataGridViewColumnSortMode.Automatic }
        );

        // Grid must be added BEFORE the filter panel so Dock.Fill fills the remaining area
        container.Controls.Add(_discoveryGrid);
        container.Controls.Add(_filterPanel);

        tab1.Controls.Add(container);
        _tabControl.TabPages.Add(tab1);

        Controls.Add(_tabControl);

        ResumeLayout(false);
        PerformLayout();
    }

    private DoubleBufferedTabControl _tabControl = null!;
    private DataGridView _discoveryGrid = null!;
    private FlowLayoutPanel _filterPanel = null!;
    private ComboBox _usernameFilter = null!;
    private ComboBox _galaxyFilter = null!;
    private ComboBox _typeFilter = null!;
    private Label _summaryLabel = null!;
}
