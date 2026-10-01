using DroidTrace.Models;
using DroidTrace.Services;

namespace DroidTrace.UI;

public sealed class MainForm : Form
{
    private readonly AdbService _adb;
    private readonly AppPaths _paths;
    private AppSettings _settings;

    private ComboBox _devices = new();
    private TextBox _case = new();
    private RichTextBox _log = new();
    private DataGridView _grid = new();
    private Label _status = new();
    private Label _deviceSummary = new();
    private Label _artifactSummary = new();
    private Label _manifestSummary = new();
    private Label _evidencePath = new();
    private Button _acquireButton = new();
    private string? _lastEvidence;

    public MainForm(AdbService adb, AppPaths paths, AppSettings settings)
    {
        _adb = adb;
        _paths = paths;
        _settings = settings;
        BuildUi();
        Shown += async (_, _) => await RefreshDevicesAsync();
    }

    private void BuildUi()
    {
        Text = "DroidTrace — Android Digital Forensic Acquisition";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9.5F);

        Controls.Add(BuildMainArea());
        Controls.Add(BuildSidebar());

        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Color.FromArgb(229, 233, 239),
            Padding = new Padding(12, 4, 12, 4)
        };
        _status = new Label
        {
            Text = "Checking ADB…",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(65, 73, 86),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        statusBar.Controls.Add(_status);
        Controls.Add(statusBar);
    }

    private Control BuildMainArea()
    {
        var main = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(20, 16, 20, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));

        layout.Controls.Add(BuildHeader(), 0, 0);
        layout.Controls.Add(BuildToolbar(), 0, 1);
        layout.Controls.Add(BuildSummaryCards(), 0, 2);
        layout.Controls.Add(BuildArtifactPanel(), 0, 3);
        layout.Controls.Add(BuildLogPanel(), 0, 4);
        main.Controls.Add(layout);
        return main;
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = Color.FromArgb(24, 29, 39),
            Padding = new Padding(14, 14, 14, 12)
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));

        var branding = new Panel { Dock = DockStyle.Fill };
        branding.Controls.Add(new Label
        {
            Text = "DROID\nTRACE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 23, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 82,
            TextAlign = ContentAlignment.MiddleLeft
        });
        branding.Controls.Add(new Label
        {
            Text = "ANDROID FORENSIC\nACQUISITION & ANALYSIS",
            ForeColor = Color.FromArgb(165, 174, 190),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Dock = DockStyle.Bottom,
            Height = 48,
            TextAlign = ContentAlignment.TopLeft
        });

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            Height = 250,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0)
        };
        for (var i = 0; i < 5; i++) nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        AddNav(nav, 0, "DASHBOARD", null);
        AddNav(nav, 1, "ACQUIRE", StartAcquisition);
        AddNav(nav, 2, "TIMELINE", ShowTimeline);
        AddNav(nav, 3, "EXPORT", ExportTimeline);
        AddNav(nav, 4, "SETTINGS", ShowSettings);

        var footer = new Label
        {
            Text = "AUTHORIZED USE ONLY\n\nCollect and examine only devices\nyou are legally authorized to analyze.",
            ForeColor = Color.FromArgb(132, 141, 157),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = true
        };

        sidebarLayout.Controls.Add(branding, 0, 0);
        sidebarLayout.Controls.Add(nav, 0, 1);
        sidebarLayout.Controls.Add(footer, 0, 2);
        sidebar.Controls.Add(sidebarLayout);
        return sidebar;
    }

    private void AddNav(TableLayoutPanel nav, int row, string text, EventHandler? handler)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31, 38, 51),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(13, 0, 0, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(62, 72, 90);
        button.FlatAppearance.BorderSize = 1;
        button.Click += handler ?? ((_, _) => { });
        nav.Controls.Add(button, 0, row);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(new Label
        {
            Text = "Acquisition Dashboard",
            Font = new Font("Segoe UI", 21, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 32, 44),
            Dock = DockStyle.Top,
            Height = 34,
            AutoEllipsis = true
        });
        panel.Controls.Add(new Label
        {
            Text = "Acquire, preserve and analyze Android artifacts with evidence integrity.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(105, 115, 130),
            Dock = DockStyle.Bottom,
            Height = 24,
            AutoEllipsis = true
        });
        return panel;
    }

    private Control BuildToolbar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10, 8, 10, 8) };
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69));

        _devices = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 8, 0)
        };
        bar.Controls.Add(_devices, 0, 0);

        var refresh = new Button
        {
            Text = "Refresh",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 0)
        };
        refresh.Click += async (_, _) => await RefreshDevicesAsync();
        bar.Controls.Add(refresh, 1, 0);

        var casePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 8, 0) };
        casePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
        casePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        casePanel.Controls.Add(new Label { Text = "Case ID", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _case = new TextBox { Dock = DockStyle.Fill, Text = "CASE-" + DateTime.Now.ToString("yyyyMMdd") };
        casePanel.Controls.Add(_case, 1, 0);
        bar.Controls.Add(casePanel, 2, 0);

        _acquireButton = new Button
        {
            Text = "Acquire Evidence",
            Dock = DockStyle.Left,
            Width = 145,
            BackColor = Color.FromArgb(25, 103, 210),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0)
        };
        _acquireButton.FlatAppearance.BorderSize = 0;
        _acquireButton.Click += StartAcquisition;
        bar.Controls.Add(_acquireButton, 3, 0);
        bar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 4, 0);
        panel.Controls.Add(bar);
        return panel;
    }

    private Control BuildSummaryCards()
    {
        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 4)
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        cards.Controls.Add(Card("DEVICE", "No device", ref _deviceSummary, new Padding(0, 0, 8, 0)), 0, 0);
        cards.Controls.Add(Card("ARTIFACT SETS", "0", ref _artifactSummary, new Padding(0, 0, 8, 0)), 1, 0);
        cards.Controls.Add(Card("MANIFEST SHA-256", "Not acquired", ref _manifestSummary, new Padding(0)), 2, 0);
        return cards;
    }

    private Panel Card(string title, string value, ref Label valueLabel, Padding margin)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = margin,
            Padding = new Padding(14, 8, 14, 8)
        };
        card.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 19,
            ForeColor = Color.FromArgb(105, 115, 130),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            AutoEllipsis = true
        });
        valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(30, 38, 51),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(valueLabel);
        return card;
    }

    private Control BuildArtifactPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 44, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label { Text = "Evidence Artifacts", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _evidencePath = new Label { Text = "No acquisition selected", ForeColor = Color.FromArgb(110, 119, 133), Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8, 0, 0, 0) };
        header.Controls.Add(_evidencePath, 1, 0);
        panel.Controls.Add(header);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.FromArgb(225, 229, 235),
            AutoGenerateColumns = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 32 },
            ScrollBars = ScrollBars.Both
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 34;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(237, 240, 244),
            ForeColor = Color.FromArgb(55, 65, 81),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Padding = new Padding(6, 0, 6, 0)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(45, 55, 72),
            SelectionBackColor = Color.FromArgb(221, 235, 252),
            SelectionForeColor = Color.FromArgb(20, 30, 45),
            Padding = new Padding(6, 0, 6, 0),
            WrapMode = DataGridViewTriState.False
        };
        panel.Controls.Add(_grid);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
        panel.Controls.Add(new Label
        {
            Text = "Activity Log",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81)
        });
        _log = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(249, 250, 251),
            ForeColor = Color.FromArgb(65, 73, 86),
            Font = new Font("Consolas", 8.5F),
            DetectUrls = true,
            WordWrap = false
        };
        panel.Controls.Add(_log);
        return panel;
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var ds = await _adb.GetDevicesAsync();
            _devices.Items.Clear();
            foreach (var d in ds) _devices.Items.Add(d);
            _devices.DisplayMember = nameof(DeviceInfo.Serial);
            if (_devices.Items.Count > 0) _devices.SelectedIndex = 0;

            _deviceSummary.Text = ds.Count == 0 ? "No authorized device" : ds.Count == 1 ? ds[0].Serial : $"{ds.Count} devices detected";
            _status.Text = ds.Count == 0 ? "ADB connected • No authorized Android device" : $"ADB connected • {ds.Count} device(s) detected";
            Log(ds.Count == 0 ? "No authorized Android device detected." : $"Detected {ds.Count} authorized Android device(s).");
        }
        catch (Exception ex)
        {
            _deviceSummary.Text = "ADB unavailable";
            _status.Text = "ADB error • " + ex.Message;
            Log("ADB ERROR: " + ex.Message);
        }
    }

    private async void StartAcquisition(object? sender, EventArgs e)
    {
        if (_devices.SelectedItem is not DeviceInfo d)
        {
            MessageBox.Show("Connect and authorize an Android device first.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_case.Text))
        {
            MessageBox.Show("Enter a case ID.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Toggle(false);
        try
        {
            Log($"Starting acquisition for {d.Serial} / case {_case.Text.Trim()} …");
            var root = Path.Combine(_paths.BaseDirectory, _settings.EvidenceRoot);
            var p = new Progress<string>(s => Log(s));
            var m = await new AcquisitionService(_adb).AcquireAsync(d.Serial, _case.Text.Trim(), root, p);
            _lastEvidence = Directory.GetDirectories(root).OrderByDescending(x => x).FirstOrDefault();
            _artifactSummary.Text = m.Artifacts.Count.ToString();
            _manifestSummary.Text = m.ManifestSha256[..Math.Min(12, m.ManifestSha256.Length)] + "…";
            _evidencePath.Text = _lastEvidence ?? "Evidence path unavailable";
            _status.Text = $"Acquisition complete • {m.Artifacts.Count} artifact sets • Manifest SHA-256 {m.ManifestSha256[..Math.Min(12, m.ManifestSha256.Length)]}…";
            LoadArtifacts(m);
            Log("Acquisition completed successfully.");
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Toggle(true);
        }
    }

    private void LoadArtifacts(EvidenceManifest m)
    {
        _grid.Columns.Clear();
        AddTextColumn("Type", "Type", 0, 150);
        AddTextColumn("File", "FileName", 1, 190);
        AddTextColumn("Records", "Records", 2, 80);
        AddTextColumn("Status", "Success", 3, 80);
        AddTextColumn("SHA-256", "Sha256", 4, 360);
        AddTextColumn("Acquired", "AcquiredAt", 5, 180);
        _grid.DataSource = m.Artifacts.Select(a => new { a.Type, a.FileName, a.Records, Success = a.Success ? "OK" : "FAILED", a.Sha256, AcquiredAt = a.AcquiredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") }).ToList();
    }

    private void ShowTimeline(object? s, EventArgs e)
    {
        if (_lastEvidence == null) { MessageBox.Show("Acquire evidence first.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var events = TimelineService.Build(_lastEvidence);
        _grid.Columns.Clear();
        AddTextColumn("Timestamp", "Timestamp", 0, 190);
        AddTextColumn("Artifact", "Artifact", 1, 150);
        AddTextColumn("Summary", "Summary", 2, 520);
        AddTextColumn("Source", "Source", 3, 300);
        _grid.DataSource = events.Select(x => new { Timestamp = x.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), Artifact = x.Artifact, Summary = x.Summary, Source = x.Source }).ToList();
        _status.Text = $"Timeline loaded • {events.Count} timestamped events";
        Log($"Timeline contains {events.Count} timestamped events.");
    }

    private async void ExportTimeline(object? s, EventArgs e)
    {
        if (_lastEvidence == null) { MessageBox.Show("Acquire evidence first.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var d = new SaveFileDialog { Filter = "CSV|*.csv|JSON|*.json", FileName = "droidtrace-timeline.csv", Title = "Export DroidTrace Timeline" };
        if (d.ShowDialog() != DialogResult.OK) return;
        var ev = TimelineService.Build(_lastEvidence);
        if (Path.GetExtension(d.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)) await ExportService.ExportJsonAsync(d.FileName, ev);
        else await ExportService.ExportCsvAsync(d.FileName, ev);
        Log("Exported " + d.FileName);
        _status.Text = "Timeline exported • " + Path.GetFileName(d.FileName);
    }

    private void ShowSettings(object? s, EventArgs e)
    {
        using var f = new Form { Text = "DroidTrace Settings", Width = 700, Height = 360, MinimumSize = new Size(650, 330), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(245, 247, 250), Font = new Font("Segoe UI", 9.5F) };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, Height = 205, ColumnCount = 2, RowCount = 3, Padding = new Padding(20), AutoSize = false };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var adb = new TextBox { Dock = DockStyle.Fill, Text = _settings.AdbPath };
        var pg = new TextBox { Dock = DockStyle.Fill, Text = _settings.PostgreSqlConnection, UseSystemPasswordChar = false };
        var ev = new TextBox { Dock = DockStyle.Fill, Text = _settings.EvidenceRoot };
        AddSettingRow(table, 0, "ADB path", adb);
        AddSettingRow(table, 1, "PostgreSQL", pg);
        AddSettingRow(table, 2, "Evidence root", ev);
        f.Controls.Add(table);

        var save = new Button { Text = "Save Settings", Width = 120, Height = 34, Anchor = AnchorStyles.Right | AnchorStyles.Top, Left = 530, Top = 230, BackColor = Color.FromArgb(25, 103, 210), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => { _settings = new(adb.Text.Trim(), pg.Text.Trim(), ev.Text.Trim()); SettingsService.Save(_paths.SettingsFile, _settings); MessageBox.Show("Settings saved. Restart DroidTrace to apply ADB path changes.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information); f.Close(); };
        f.Controls.Add(save);
        f.ShowDialog(this);
    }

    private static void AddSettingRow(TableLayoutPanel table, int row, string label, Control editor)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(55, 65, 81) }, 0, row);
        table.Controls.Add(editor, 1, row);
    }

    private void AddTextColumn(string header, string property, int index, int width)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Name = property,
            MinimumWidth = Math.Max(60, Math.Min(width, 180)),
            FillWeight = Math.Max(5, width),
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        _grid.Columns.Add(column);
    }

    private void Log(string s)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}{Environment.NewLine}");
        _log.ScrollToCaret();
    }

    private void Toggle(bool enabled)
    {
        foreach (Control c in Controls) c.Enabled = enabled;
        _status.Enabled = true;
        _acquireButton.Enabled = enabled;
    }

    public override string ToString() => "DroidTrace";
}
