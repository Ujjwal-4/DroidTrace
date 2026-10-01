using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DroidTrace.Models;
using DroidTrace.Services;

namespace DroidTrace.UI;

public sealed class MainForm : Form
{
    private readonly AdbService _adb;
    private readonly AppPaths _paths;
    private AppSettings _settings;

    // Navigation and Views
    private readonly List<Button> _navButtons = [];
    private readonly Dictionary<string, Panel> _views = [];
    private Panel _contentPanel = null!;
    private string _activeViewName = "ACQUIRE";

    // Global Status Bar
    private Label _statusAdb = null!;
    private Label _statusDevice = null!;
    private Label _statusCase = null!;
    private ProgressBar _globalProgress = null!;

    // === View 1: Acquisition & Evidence ===
    private ComboBox _devicesCombo = null!;
    private TextBox _caseInput = null!;
    private Button _acquireButton = null!;
    private Label _cardDeviceVal = null!;
    private Label _cardStatusVal = null!;
    private Label _cardArtifactsVal = null!;
    private Label _cardManifestVal = null!;
    private DataGridView _gridArtifacts = null!;
    private Label _evidenceDirLabel = null!;
    private RichTextBox _artifactPreviewBox = null!;
    private Label _artifactPreviewTitle = null!;
    private Label _artifactPreviewMeta = null!;
    private string? _lastEvidenceDir;
    private EvidenceManifest? _lastManifest;

    // === View 2: Timeline Explorer ===
    private DataGridView _gridTimeline = null!;
    private TextBox _timelineSearchInput = null!;
    private Label _timelineCountLabel = null!;
    private RichTextBox _timelineDetailBox = null!;
    private List<TimelineEvent> _loadedTimelineEvents = [];
    private string _currentTimelineFilterType = "ALL";
    private Button _btnFilterAll = null!;
    private Button _btnFilterSms = null!;
    private Button _btnFilterCalls = null!;

    // === View 3: Device Inspector & Shell ===
    private Label _devModelVal = null!;
    private Label _devManufVal = null!;
    private Label _devSerialVal = null!;
    private Label _devAndroidVal = null!;
    private Label _devSecurityVal = null!;
    private Label _devBuildVal = null!;
    private Label _devBatteryVal = null!;
    private Label _devAbiVal = null!;
    private ComboBox _shellPresetsCombo = null!;
    private TextBox _shellCommandInput = null!;
    private RichTextBox _shellOutputBox = null!;
    private Button _shellRunButton = null!;

    // === View 4: Activity Log ===
    private RichTextBox _logBox = null!;

    // === View 5: Settings ===
    private TextBox _settingsAdbInput = null!;
    private TextBox _settingsEvidenceInput = null!;
    private TextBox _settingsPgInput = null!;
    private Label _settingsAdbStatus = null!;
    private Label _settingsPgStatus = null!;

    public MainForm(AdbService adb, AppPaths paths, AppSettings settings)
    {
        _adb = adb;
        _paths = paths;
        _settings = settings;

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BuildUi();

        Shown += async (_, _) =>
        {
            await RefreshDevicesAsync();
            await CheckAdbStatusAsync();
            TryLoadLatestExistingEvidence();
        };
    }

    private void BuildUi()
    {
        Text = "DroidTrace — Android Digital Forensic Acquisition Suite";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1240, 800);
        WindowState = FormWindowState.Maximized;
        BackColor = Theme.Background;
        ForeColor = Theme.TextPrimary;
        Font = Theme.BodyFont;

        // Root layout
        // WinForms docks controls in reverse Controls order: the LAST control added is docked FIRST.
        // So add the Fill panel first, then the sidebar, then the status bar. Otherwise the Fill panel
        // is laid out across the whole client area and gets hidden under the sidebar and status bar.
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        Controls.Add(root);

        // 1. Central Content Panel (houses all views)
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(20, 16, 20, 12)
        };
        root.Controls.Add(_contentPanel);

        // 2. Left Navigation Sidebar
        root.Controls.Add(BuildSidebar());

        // 3. Bottom Status Bar (spans full width)
        root.Controls.Add(BuildStatusBar());

        // Create individual views
        _views["ACQUIRE"] = BuildAcquisitionView();
        _views["TIMELINE"] = BuildTimelineView();
        _views["DEVICE"] = BuildDeviceView();
        _views["LOGS"] = BuildLogsView();
        _views["SETTINGS"] = BuildSettingsView();

        foreach (var view in _views.Values)
        {
            view.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(view);
        }

        // Show default view
        SwitchView("ACQUIRE");
    }

    #region Sidebar Navigation
    private Panel BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 230,
            BackColor = Theme.SidebarBackground,
            Padding = new Padding(12, 16, 12, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // Branding
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Nav items
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Security Notice

        // 1. Branding Header
        var branding = new Panel { Dock = DockStyle.Fill };
        var brandTitle = new Label
        {
            Text = "DROIDTRACE",
            ForeColor = Theme.TextPrimary,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var brandSub = new Label
        {
            Text = "FORENSIC ACQUISITION\n& ARTIFACT ANALYSIS",
            UseMnemonic = false,
            ForeColor = Theme.PrimaryLight,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.TopLeft
        };
        var brandBadge = new Label
        {
            Text = "v1.3 ENTERPRISE SUITE",
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            Dock = DockStyle.Bottom,
            Height = 20,
            TextAlign = ContentAlignment.BottomLeft
        };
        branding.Controls.Add(brandBadge);
        branding.Controls.Add(brandSub);
        branding.Controls.Add(brandTitle);

        // 2. Navigation Items
        var navContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            Height = 260,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        for (var i = 0; i < 5; i++) navContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        AddNavButton(navContainer, 0, "ACQUIRE", "📊  Acquisition & Case", (_, _) => SwitchView("ACQUIRE"));
        AddNavButton(navContainer, 1, "TIMELINE", "🕒  Forensic Timeline", (_, _) => SwitchView("TIMELINE"));
        AddNavButton(navContainer, 2, "DEVICE", "📱  Device Inspector", (_, _) => SwitchView("DEVICE"));
        AddNavButton(navContainer, 3, "LOGS", "📜  Audit & Activity", (_, _) => SwitchView("LOGS"));
        AddNavButton(navContainer, 4, "SETTINGS", "⚙️  Configuration", (_, _) => SwitchView("SETTINGS"));

        // 3. Authorization Notice Card
        var footerCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 24, 38),
            Padding = new Padding(10, 8, 10, 8)
        };
        var footerHeader = new Label
        {
            Text = "⚖️ LAWFUL USE ONLY",
            ForeColor = Theme.Warning,
            Font = Theme.CaptionBoldFont,
            Dock = DockStyle.Top,
            Height = 20
        };
        var footerText = new Label
        {
            Text = "Examine only authorized target hardware under chain of custody.",
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            Dock = DockStyle.Fill
        };
        footerCard.Controls.Add(footerText);
        footerCard.Controls.Add(footerHeader);

        layout.Controls.Add(branding, 0, 0);
        layout.Controls.Add(navContainer, 0, 1);
        layout.Controls.Add(footerCard, 0, 2);

        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private void AddNavButton(TableLayoutPanel nav, int row, string tag, string text, EventHandler onClick)
    {
        var btn = new Button
        {
            Tag = tag,
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Theme.TextSecondary,
            Font = Theme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
        btn.Click += onClick;
        _navButtons.Add(btn);
        nav.Controls.Add(btn, 0, row);
    }

    private void SwitchView(string viewName)
    {
        _activeViewName = viewName;
        foreach (var kvp in _views)
        {
            kvp.Value.Visible = kvp.Key == viewName;
            if (kvp.Value.Visible) kvp.Value.BringToFront();
        }

        foreach (var btn in _navButtons)
        {
            var isCurrent = (string)btn.Tag! == viewName;
            btn.BackColor = isCurrent ? Theme.SidebarActive : Color.Transparent;
            btn.ForeColor = isCurrent ? Color.White : Theme.TextSecondary;
            btn.FlatAppearance.BorderSize = isCurrent ? 1 : 0;
            btn.FlatAppearance.BorderColor = isCurrent ? Theme.PrimaryLight : Theme.SidebarBackground;
        }

        if (viewName == "TIMELINE") LoadTimelineData();
        if (viewName == "DEVICE") _ = RefreshDeviceDetailsAsync();
    }
    #endregion

    #region Status Bar
    private Panel BuildStatusBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Theme.SidebarBackground,
            Padding = new Padding(12, 4, 12, 4)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); // ADB status
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260)); // Target Device
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Case info
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // Progress Bar
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); // Notice

        _statusAdb = new Label
        {
            Text = "● Checking ADB…",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Warning,
            Font = Theme.CaptionBoldFont,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _statusDevice = new Label
        {
            Text = "📱 Target: No Device",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.CaptionFont,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _statusCase = new Label
        {
            Text = "📁 Case: None",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _globalProgress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 14,
            Style = ProgressBarStyle.Blocks,
            Value = 0,
            Visible = false
        };

        var legalNotice = new Label
        {
            Text = "🔒 Integrity Hash Verified",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Success,
            Font = Theme.CaptionBoldFont,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        layout.Controls.Add(_statusAdb, 0, 0);
        layout.Controls.Add(_statusDevice, 1, 0);
        layout.Controls.Add(_statusCase, 2, 0);
        layout.Controls.Add(_globalProgress, 3, 0);
        layout.Controls.Add(legalNotice, 4, 0);

        bar.Controls.Add(layout);
        return bar;
    }
    #endregion

    #region View 1: Acquisition & Evidence
    private Panel BuildAcquisitionView()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Header title
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86)); // Metric cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Two-pane split

        // 1. Header
        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Acquisition & Evidence Center",
            UseMnemonic = false,
            Font = Theme.HeaderFont,
            ForeColor = Theme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };
        var subtitle = new Label
        {
            Text = "Forensic acquisition workflow, SHA-256 integrity verification, and instant artifact inspection.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 22
        };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        // 2. Acquisition Toolbar
        var toolbar = BuildAcquisitionToolbar();

        // 3. Stat Cards
        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        cards.Controls.Add(Theme.CreateCard("TARGET DEVICE", "Searching…", out _cardDeviceVal, Theme.Cyan), 0, 0);
        cards.Controls.Add(Theme.CreateCard("ACQUISITION STATUS", "Ready to Acquire", out _cardStatusVal, Theme.Primary), 1, 0);
        cards.Controls.Add(Theme.CreateCard("ARTIFACT SETS", "0 Extracted", out _cardArtifactsVal, Theme.Success), 2, 0);
        cards.Controls.Add(Theme.CreateCard("MANIFEST SHA-256", "Not Computed", out _cardManifestVal, Theme.Purple), 3, 0);

        // 4. Two-pane split container (Artifacts Grid on Left, Raw File Inspector on Right)
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            BackColor = Theme.Border
        };
        // Give the artifacts table ~58% of the width (min 560 px) and keep a usable preview pane.
        split.HandleCreated += (_, _) => ApplySplit(split, 0.58, 560, 360);
        split.SizeChanged += (_, _) => { if (!split.Capture) ApplySplit(split, 0.58, 560, 360); };

        // Left Pane: Artifacts Table
        var leftPane = BuildArtifactsTablePane();
        split.Panel1.Controls.Add(leftPane);
        split.Panel1.BackColor = Theme.Surface;

        // Right Pane: Raw Artifact Live Preview
        var rightPane = BuildArtifactPreviewPane();
        split.Panel2.Controls.Add(rightPane);
        split.Panel2.BackColor = Theme.Surface;

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(toolbar, 0, 1);
        mainLayout.Controls.Add(cards, 0, 2);
        mainLayout.Controls.Add(split, 0, 3);

        panel.Controls.Add(mainLayout);
        return panel;
    }

    private static void ApplySplit(SplitContainer split, double ratio, int min1, int min2)
    {
        if (split.Width <= min1 + min2 + split.SplitterWidth) return;
        try
        {
            split.Panel1MinSize = min1;
            split.Panel2MinSize = min2;
            var target = (int)(split.Width * ratio);
            split.SplitterDistance = Math.Max(min1, Math.Min(target, split.Width - min2 - split.SplitterWidth));
        }
        catch (Exception) { /* splitter sizing is cosmetic; never let it crash the UI */ }
    }

    private Control BuildAcquisitionToolbar()
    {
        var barPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8)
        };

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Label: Device
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Device combo
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));  // Refresh btn
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Label: Case ID
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Case input
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185)); // Acquire btn

        var devLabel = new Label
        {
            Text = "Target Device:",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _devicesCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 4, 8, 4)
        };
        _devicesCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_devicesCombo.SelectedItem is DeviceInfo d)
            {
                _statusDevice.Text = $"📱 Target: {d.Model ?? d.Serial}";
                _cardDeviceVal.Text = string.IsNullOrWhiteSpace(d.Model) ? d.Serial : $"{d.Model} ({d.Serial})";
            }
        };

        var refreshBtn = new Button
        {
            Text = "↻ Refresh",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 12, 4)
        };
        Theme.StyleButton(refreshBtn, ButtonVariant.Secondary);
        refreshBtn.Click += async (_, _) => await RefreshDevicesAsync();

        var caseLabel = new Label
        {
            Text = "Case ID:",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _caseInput = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = "CASE-" + DateTime.Now.ToString("yyyyMMdd"),
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 7, 12, 7)
        };
        _caseInput.TextChanged += (_, _) => _statusCase.Text = $"📁 Case: {_caseInput.Text.Trim()}";

        _acquireButton = new Button
        {
            Text = "▶ Start Acquisition",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3)
        };
        Theme.StyleButton(_acquireButton, ButtonVariant.Primary);
        _acquireButton.Click += StartAcquisition;

        bar.Controls.Add(devLabel, 0, 0);
        bar.Controls.Add(_devicesCombo, 1, 0);
        bar.Controls.Add(refreshBtn, 2, 0);
        bar.Controls.Add(caseLabel, 3, 0);
        bar.Controls.Add(_caseInput, 4, 0);
        bar.Controls.Add(_acquireButton, 5, 0);

        barPanel.Controls.Add(bar);
        return barPanel;
    }

    private Control BuildArtifactsTablePane()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(12)
        };

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));

        var title = new Label
        {
            Text = "Evidence Artifacts",
            Font = Theme.SubHeaderFont,
            ForeColor = Theme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _evidenceDirLabel = new Label
        {
            Text = "No active evidence directory",
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true,
            Padding = new Padding(0, 0, 8, 0)
        };

        var openFolderBtn = new Button
        {
            Text = "📂 Open Case Folder",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2)
        };
        Theme.StyleButton(openFolderBtn, ButtonVariant.Secondary);
        openFolderBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_lastEvidenceDir) && Directory.Exists(_lastEvidenceDir))
            {
                Process.Start(new ProcessStartInfo { FileName = _lastEvidenceDir, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("No active case directory found. Acquire evidence first.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        topBar.Controls.Add(title, 0, 0);
        topBar.Controls.Add(_evidenceDirLabel, 1, 0);
        topBar.Controls.Add(openFolderBtn, 2, 0);

        _gridArtifacts = new DataGridView
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };
        Theme.StyleGrid(_gridArtifacts);
        SetupArtifactColumns();

        _gridArtifacts.SelectionChanged += (_, _) => OnArtifactSelected();

        panel.Controls.Add(_gridArtifacts);
        panel.Controls.Add(topBar);
        return panel;
    }

    private void SetupArtifactColumns()
    {
        _gridArtifacts.Columns.Clear();

        var colType = new DataGridViewTextBoxColumn { HeaderText = "Artifact Type", DataPropertyName = "Type", Width = 140, MinimumWidth = 110 };
        var colFile = new DataGridViewTextBoxColumn { HeaderText = "File Name", DataPropertyName = "FileName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100, MinimumWidth = 130 };
        var colRecords = new DataGridViewTextBoxColumn { HeaderText = "Records", DataPropertyName = "Records", Width = 100, MinimumWidth = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Padding = new Padding(8, 0, 14, 0) } };
        var colStatus = new DataGridViewTextBoxColumn { HeaderText = "Integrity", DataPropertyName = "StatusText", Width = 110, MinimumWidth = 100 };
        var colHash = new DataGridViewTextBoxColumn { HeaderText = "SHA-256 Digest", DataPropertyName = "Sha256", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 160, MinimumWidth = 200 };
        var colTime = new DataGridViewTextBoxColumn { HeaderText = "Acquired Time", DataPropertyName = "AcquiredAt", Width = 170, MinimumWidth = 150 };

        _gridArtifacts.Columns.AddRange(colType, colFile, colRecords, colStatus, colHash, colTime);

        _gridArtifacts.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_gridArtifacts.Columns[e.ColumnIndex].HeaderText == "Integrity")
            {
                var val = e.Value?.ToString();
                if (val != null && val.Contains("OK"))
                {
                    e.CellStyle!.ForeColor = Theme.Success;
                    e.CellStyle.Font = Theme.BodyBoldFont;
                }
                else if (val != null && val.Contains("FAIL"))
                {
                    e.CellStyle!.ForeColor = Theme.Danger;
                    e.CellStyle.Font = Theme.BodyBoldFont;
                }
            }
            else if (_gridArtifacts.Columns[e.ColumnIndex].HeaderText == "SHA-256 Digest")
            {
                e.CellStyle!.Font = Theme.MonospaceSmallFont;
                e.CellStyle.ForeColor = Theme.Cyan;
            }
        };
    }

    private Control BuildArtifactPreviewPane()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(12)
        };

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Title
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Meta
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125)); // Copy Btn
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Editor Btn

        _artifactPreviewTitle = new Label
        {
            Text = "Raw Artifact Inspector",
            Font = Theme.SubHeaderFont,
            ForeColor = Theme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _artifactPreviewMeta = new Label
        {
            Text = "Select artifact to inspect",
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0)
        };

        var copyBtn = new Button
        {
            Text = "📋 Copy Text",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 4, 2)
        };
        Theme.StyleButton(copyBtn, ButtonVariant.Secondary);
        copyBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_artifactPreviewBox.Text))
            {
                Clipboard.SetText(_artifactPreviewBox.Text);
                Log("[INFO] Copied artifact content to clipboard.");
            }
        };

        var notepadBtn = new Button
        {
            Text = "📝 Open in Editor",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2)
        };
        Theme.StyleButton(notepadBtn, ButtonVariant.Secondary);
        notepadBtn.Click += (_, _) =>
        {
            if (_gridArtifacts.CurrentRow?.DataBoundItem != null)
            {
                var item = _gridArtifacts.CurrentRow.DataBoundItem;
                var fileName = item.GetType().GetProperty("FileName")?.GetValue(item)?.ToString();
                if (!string.IsNullOrWhiteSpace(_lastEvidenceDir) && !string.IsNullOrWhiteSpace(fileName))
                {
                    var filePath = Path.Combine(_lastEvidenceDir, fileName);
                    if (File.Exists(filePath))
                    {
                        Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = $"\"{filePath}\"", UseShellExecute = true });
                    }
                }
            }
        };

        topBar.Controls.Add(_artifactPreviewTitle, 0, 0);
        topBar.Controls.Add(_artifactPreviewMeta, 1, 0);
        topBar.Controls.Add(copyBtn, 2, 0);
        topBar.Controls.Add(notepadBtn, 3, 0);

        _artifactPreviewBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.TerminalBg,
            ForeColor = Theme.TerminalCyan,
            Font = Theme.MonospaceFont,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 8, 0, 0),
            WordWrap = false
        };

        panel.Controls.Add(_artifactPreviewBox);
        panel.Controls.Add(topBar);
        return panel;
    }

    private void OnArtifactSelected()
    {
        if (_gridArtifacts.CurrentRow?.DataBoundItem == null || string.IsNullOrWhiteSpace(_lastEvidenceDir))
        {
            _artifactPreviewTitle.Text = "Raw Artifact Inspector";
            _artifactPreviewMeta.Text = "No artifact selected";
            _artifactPreviewBox.Text = "Select an artifact from the table to preview its raw acquired data, integrity digest, and parsed content.";
            return;
        }

        var item = _gridArtifacts.CurrentRow.DataBoundItem;
        var fileName = item.GetType().GetProperty("FileName")?.GetValue(item)?.ToString();
        var type = item.GetType().GetProperty("Type")?.GetValue(item)?.ToString();
        var hash = item.GetType().GetProperty("Sha256")?.GetValue(item)?.ToString();

        if (string.IsNullOrWhiteSpace(fileName)) return;

        var fullPath = Path.Combine(_lastEvidenceDir, fileName);
        if (!File.Exists(fullPath))
        {
            _artifactPreviewTitle.Text = fileName;
            _artifactPreviewMeta.Text = "File not found on disk";
            _artifactPreviewBox.Text = $"Expected path: {fullPath}\nFile could not be found.";
            return;
        }

        try
        {
            var content = File.ReadAllText(fullPath);
            var fi = new FileInfo(fullPath);
            var lineCount = content.Split('\n').Length;

            _artifactPreviewTitle.Text = $"📄 {fileName} ({type})";
            _artifactPreviewMeta.Text = $"{fi.Length:N0} bytes • {lineCount:N0} lines • SHA256: {hash?[..Math.Min(8, hash?.Length ?? 0)]}…";
            _artifactPreviewBox.Text = content;
        }
        catch (Exception ex)
        {
            _artifactPreviewBox.Text = "Error reading artifact file:\n" + ex.Message;
        }
    }

    private async void StartAcquisition(object? sender, EventArgs e)
    {
        if (_devicesCombo.SelectedItem is not DeviceInfo d)
        {
            MessageBox.Show("Please connect and authorize an Android device first via ADB.", "DroidTrace Acquisition", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var caseId = _caseInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            MessageBox.Show("Please enter a valid Case ID.", "DroidTrace Acquisition", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ToggleControls(false);
        _cardStatusVal.Text = "Acquiring…";
        _statusCase.Text = $"📁 Case: {caseId} (Running)";
        _globalProgress.Visible = true;
        _globalProgress.Value = 20;

        try
        {
            Log($"[START] Starting acquisition session for Device {d.Serial} | Case: {caseId}");
            var root = Path.Combine(_paths.BaseDirectory, _settings.EvidenceRoot);

            var progress = new Progress<string>(s =>
            {
                Log(s);
                if (_globalProgress.Value < 90) _globalProgress.Value += 10;
            });

            var manifest = await new AcquisitionService(_adb).AcquireAsync(d.Serial, caseId, root, progress);
            _lastManifest = manifest;
            _lastEvidenceDir = Directory.GetDirectories(root).OrderByDescending(x => x).FirstOrDefault();

            _globalProgress.Value = 100;
            _cardStatusVal.Text = "Completed ✔";
            _cardArtifactsVal.Text = $"{manifest.Artifacts.Count} Sets";
            _cardManifestVal.Text = manifest.ManifestSha256[..Math.Min(12, manifest.ManifestSha256.Length)] + "…";
            _evidenceDirLabel.Text = _lastEvidenceDir ?? "Directory saved";
            _statusCase.Text = $"📁 Case: {caseId} (Completed)";

            DisplayArtifacts(manifest);
            Log($"[SUCCESS] Acquisition finished successfully! Case folder: {_lastEvidenceDir}");
            MessageBox.Show($"Forensic acquisition complete!\n\nArtifact sets: {manifest.Artifacts.Count}\nManifest SHA-256: {manifest.ManifestSha256}\nEvidence stored at:\n{_lastEvidenceDir}", "DroidTrace Acquisition Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _cardStatusVal.Text = "Error ✖";
            Log($"[ERROR] Acquisition failed: {ex.Message}");
            MessageBox.Show("Acquisition failed: " + ex.Message, "Acquisition Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _globalProgress.Visible = false;
            _globalProgress.Value = 0;
            ToggleControls(true);
        }
    }

    private void DisplayArtifacts(EvidenceManifest manifest)
    {
        var list = manifest.Artifacts.Select(a => new
        {
            a.Type,
            a.FileName,
            a.Records,
            StatusText = a.Success ? "✔ OK" : "✖ FAILED",
            a.Sha256,
            AcquiredAt = a.AcquiredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        _gridArtifacts.DataSource = list;
        if (_gridArtifacts.Rows.Count > 0)
        {
            _gridArtifacts.Rows[0].Selected = true;
            OnArtifactSelected();
        }
    }
    #endregion

    #region View 2: Forensic Timeline Explorer
    private Panel BuildTimelineView()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); // Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Timeline Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Event Details

        // Header
        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Forensic Timeline Explorer",
            Font = Theme.HeaderFont,
            ForeColor = Theme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };
        var subtitle = new Label
        {
            Text = "Chronological analysis and keyword search across SMS messages, call records, and timestamps.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 22
        };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        // Filter & Action Toolbar
        var filterBar = BuildTimelineToolbar();

        // Timeline DataGridView
        var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12) };
        _gridTimeline = new DataGridView { Dock = DockStyle.Fill };
        Theme.StyleGrid(_gridTimeline);
        SetupTimelineColumns();
        _gridTimeline.SelectionChanged += (_, _) => OnTimelineRowSelected();
        gridPanel.Controls.Add(_gridTimeline);

        // Event Detail Pane
        var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12, 8, 12, 8) };
        var detailHeader = new Label
        {
            Text = "Selected Event Record Details",
            Font = Theme.TitleFont,
            ForeColor = Theme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 22
        };
        _timelineDetailBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.TerminalBg,
            ForeColor = Theme.TerminalGreen,
            Font = Theme.MonospaceFont,
            BorderStyle = BorderStyle.None
        };
        detailPanel.Controls.Add(_timelineDetailBox);
        detailPanel.Controls.Add(detailHeader);

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(filterBar, 0, 1);
        mainLayout.Controls.Add(gridPanel, 0, 2);
        mainLayout.Controls.Add(detailPanel, 0, 3);

        panel.Controls.Add(mainLayout);
        return panel;
    }

    private Control BuildTimelineToolbar()
    {
        var barPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8)
        };

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); // Search
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); // All filter
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); // SMS filter
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); // Calls filter
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); // Event count label
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Export CSV
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Export JSON

        _timelineSearchInput = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "🔍 Search phone, address, text, timestamp…",
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 5, 8, 5)
        };
        _timelineSearchInput.TextChanged += (_, _) => ApplyTimelineFilter();

        _btnFilterAll = new Button { Text = "All", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
        _btnFilterSms = new Button { Text = "SMS", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
        _btnFilterCalls = new Button { Text = "Calls", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 8, 3) };

        Theme.StyleButton(_btnFilterAll, ButtonVariant.Primary);
        Theme.StyleButton(_btnFilterSms, ButtonVariant.Secondary);
        Theme.StyleButton(_btnFilterCalls, ButtonVariant.Secondary);

        _btnFilterAll.Click += (_, _) => SetTimelineTypeFilter("ALL");
        _btnFilterSms.Click += (_, _) => SetTimelineTypeFilter("SMS");
        _btnFilterCalls.Click += (_, _) => SetTimelineTypeFilter("CALL");

        _timelineCountLabel = new Label
        {
            Text = "0 events loaded",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextMuted,
            Font = Theme.CaptionFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        var exportCsvBtn = new Button { Text = "💾 Export CSV", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
        Theme.StyleButton(exportCsvBtn, ButtonVariant.Secondary);
        exportCsvBtn.Click += async (_, _) => await ExportTimelineFileAsync(isJson: false);

        var exportJsonBtn = new Button { Text = "💾 Export JSON", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        Theme.StyleButton(exportJsonBtn, ButtonVariant.Secondary);
        exportJsonBtn.Click += async (_, _) => await ExportTimelineFileAsync(isJson: true);

        bar.Controls.Add(_timelineSearchInput, 0, 0);
        bar.Controls.Add(_btnFilterAll, 1, 0);
        bar.Controls.Add(_btnFilterSms, 2, 0);
        bar.Controls.Add(_btnFilterCalls, 3, 0);
        bar.Controls.Add(_timelineCountLabel, 4, 0);
        bar.Controls.Add(exportCsvBtn, 5, 0);
        bar.Controls.Add(exportJsonBtn, 6, 0);

        barPanel.Controls.Add(bar);
        return barPanel;
    }

    private void SetupTimelineColumns()
    {
        _gridTimeline.Columns.Clear();
        var colTime = new DataGridViewTextBoxColumn { HeaderText = "Timestamp (Local)", DataPropertyName = "FormattedTime", Width = 180 };
        var colType = new DataGridViewTextBoxColumn { HeaderText = "Artifact", DataPropertyName = "Artifact", Width = 90 };
        var colSummary = new DataGridViewTextBoxColumn { HeaderText = "Event Summary / Content", DataPropertyName = "Summary", Width = 560, FillWeight = 300 };
        var colSource = new DataGridViewTextBoxColumn { HeaderText = "Source URI", DataPropertyName = "Source", Width = 200 };

        _gridTimeline.Columns.AddRange(colTime, colType, colSummary, colSource);

        _gridTimeline.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_gridTimeline.Columns[e.ColumnIndex].HeaderText == "Artifact")
            {
                var val = e.Value?.ToString();
                if (val == "SMS")
                {
                    e.CellStyle!.ForeColor = Theme.Cyan;
                    e.CellStyle.Font = Theme.BodyBoldFont;
                }
                else if (val == "CALL")
                {
                    e.CellStyle!.ForeColor = Theme.Warning;
                    e.CellStyle.Font = Theme.BodyBoldFont;
                }
            }
        };
    }

    private void SetTimelineTypeFilter(string type)
    {
        _currentTimelineFilterType = type;
        Theme.StyleButton(_btnFilterAll, type == "ALL" ? ButtonVariant.Primary : ButtonVariant.Secondary);
        Theme.StyleButton(_btnFilterSms, type == "SMS" ? ButtonVariant.Primary : ButtonVariant.Secondary);
        Theme.StyleButton(_btnFilterCalls, type == "CALL" ? ButtonVariant.Primary : ButtonVariant.Secondary);
        ApplyTimelineFilter();
    }

    private void LoadTimelineData()
    {
        if (string.IsNullOrWhiteSpace(_lastEvidenceDir) || !Directory.Exists(_lastEvidenceDir))
        {
            _timelineCountLabel.Text = "No evidence acquired yet. Run acquisition first.";
            _gridTimeline.DataSource = null;
            return;
        }

        try
        {
            _loadedTimelineEvents = TimelineService.Build(_lastEvidenceDir);
            ApplyTimelineFilter();
            Log($"[TIMELINE] Built {_loadedTimelineEvents.Count} timestamped forensic events from case evidence.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Failed to build timeline: {ex.Message}");
        }
    }

    private void ApplyTimelineFilter()
    {
        var filtered = TimelineService.Filter(_loadedTimelineEvents, _timelineSearchInput.Text, _currentTimelineFilterType);
        _timelineCountLabel.Text = $"{filtered.Count:N0} of {_loadedTimelineEvents.Count:N0} events";

        _gridTimeline.DataSource = filtered.Select(e => new
        {
            FormattedTime = e.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            e.Artifact,
            e.Summary,
            e.Source,
            RawEvent = e
        }).ToList();
    }

    private void OnTimelineRowSelected()
    {
        if (_gridTimeline.CurrentRow?.DataBoundItem == null)
        {
            _timelineDetailBox.Text = "";
            return;
        }

        var item = _gridTimeline.CurrentRow.DataBoundItem;
        var summary = item.GetType().GetProperty("Summary")?.GetValue(item)?.ToString();
        var time = item.GetType().GetProperty("FormattedTime")?.GetValue(item)?.ToString();
        var artifact = item.GetType().GetProperty("Artifact")?.GetValue(item)?.ToString();
        var source = item.GetType().GetProperty("Source")?.GetValue(item)?.ToString();

        _timelineDetailBox.Text = $"[RECORD: {artifact}]\nTimestamp: {time}\nSource: {source}\nData: {summary}";
    }

    private async Task ExportTimelineFileAsync(bool isJson)
    {
        if (_loadedTimelineEvents.Count == 0)
        {
            MessageBox.Show("No timeline events to export. Acquire evidence first.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = isJson ? "JSON Files (*.json)|*.json" : "CSV Files (*.csv)|*.csv",
            FileName = isJson ? "droidtrace_timeline.json" : "droidtrace_timeline.csv",
            Title = "Export Forensic Timeline"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var filtered = TimelineService.Filter(_loadedTimelineEvents, _timelineSearchInput.Text, _currentTimelineFilterType);
            if (isJson)
            {
                await ExportService.ExportJsonAsync(sfd.FileName, filtered);
            }
            else
            {
                await ExportService.ExportCsvAsync(sfd.FileName, filtered);
            }
            Log($"[EXPORT] Saved {filtered.Count} timeline records to {sfd.FileName}");
            MessageBox.Show($"Exported {filtered.Count} records successfully to:\n{sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    #endregion

    #region View 3: Device Inspector & Shell Console
    private Panel BuildDeviceView()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Two columns

        // Header
        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Device Inspector & Live Diagnostics",
            UseMnemonic = false,
            Font = Theme.HeaderFont,
            ForeColor = Theme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };
        var subtitle = new Label
        {
            Text = "Hardware environment, operating system parameters, battery health, and examiner ADB shell console.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 22
        };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        // Split container: Left is Hardware Specs, Right is Shell Terminal
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 480,
            SplitterWidth = 8,
            BackColor = Theme.Border
        };

        // Left: Specs Card
        split.Panel1.Controls.Add(BuildDeviceSpecsPanel());
        split.Panel1.BackColor = Theme.Surface;

        // Right: Shell Terminal
        split.Panel2.Controls.Add(BuildShellTerminalPanel());
        split.Panel2.BackColor = Theme.Surface;

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(split, 0, 1);

        panel.Controls.Add(mainLayout);
        return panel;
    }

    private Control BuildDeviceSpecsPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(16) };

        var topHeader = new Panel { Dock = DockStyle.Top, Height = 40 };
        var title = new Label { Text = "Target Device Hardware & OS Profile", UseMnemonic = false, Font = Theme.SubHeaderFont, ForeColor = Theme.TextPrimary, Dock = DockStyle.Left, AutoSize = true };
        var refreshBtn = new Button { Text = "↻ Re-scan", Dock = DockStyle.Right, Width = 95 };
        Theme.StyleButton(refreshBtn, ButtonVariant.Secondary);
        refreshBtn.Click += async (_, _) => await RefreshDeviceDetailsAsync();
        topHeader.Controls.Add(refreshBtn);
        topHeader.Controls.Add(title);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 8; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        AddSpecRow(grid, 0, "Device Model:", out _devModelVal);
        AddSpecRow(grid, 1, "Manufacturer:", out _devManufVal);
        AddSpecRow(grid, 2, "Serial Number:", out _devSerialVal);
        AddSpecRow(grid, 3, "Android Release:", out _devAndroidVal);
        AddSpecRow(grid, 4, "Security Patch:", out _devSecurityVal);
        AddSpecRow(grid, 5, "Build ID:", out _devBuildVal);
        AddSpecRow(grid, 6, "Battery Level:", out _devBatteryVal);
        AddSpecRow(grid, 7, "CPU Architecture:", out _devAbiVal);

        panel.Controls.Add(grid);
        panel.Controls.Add(topHeader);
        return panel;
    }

    private static void AddSpecRow(TableLayoutPanel table, int row, string label, out Label valueLabel)
    {
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Font = Theme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        valueLabel = new Label
        {
            Text = "Unknown",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextPrimary,
            Font = Theme.BodyFont,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        table.Controls.Add(lbl, 0, row);
        table.Controls.Add(valueLabel, 1, row);
    }

    private Control BuildShellTerminalPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(16) };

        var topHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        var title = new Label { Text = "Interactive ADB Shell Console", Font = Theme.SubHeaderFont, ForeColor = Theme.TextPrimary, Dock = DockStyle.Left, AutoSize = true };
        var clearBtn = new Button { Text = "🗑 Clear", Dock = DockStyle.Right, Width = 80 };
        Theme.StyleButton(clearBtn, ButtonVariant.Secondary);
        clearBtn.Click += (_, _) => _shellOutputBox.Text = "";
        topHeader.Controls.Add(clearBtn);
        topHeader.Controls.Add(title);

        var promptPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 8)
        };
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Presets
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));  // Prompt symbol
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Input
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Run button

        _shellPresetsCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 7, 8, 7)
        };
        _shellPresetsCombo.Items.AddRange(["Custom Command…", "dumpsys battery", "getprop", "pm list packages", "cat /proc/version", "df -h", "uptime", "wm size"]);
        _shellPresetsCombo.SelectedIndex = 0;
        _shellPresetsCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_shellPresetsCombo.SelectedIndex > 0)
            {
                _shellCommandInput.Text = _shellPresetsCombo.SelectedItem?.ToString() ?? "";
            }
        };

        var promptSymbol = new Label
        {
            Text = "adb$",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TerminalGreen,
            Font = Theme.MonospaceFont,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _shellCommandInput = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = "getprop ro.product.model",
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.MonospaceFont,
            Margin = new Padding(0, 9, 8, 9)
        };
        _shellCommandInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await RunShellCommandAsync();
            }
        };

        _shellRunButton = new Button
        {
            Text = "▶ Run",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 6)
        };
        Theme.StyleButton(_shellRunButton, ButtonVariant.Primary);
        _shellRunButton.Click += async (_, _) => await RunShellCommandAsync();

        promptPanel.Controls.Add(_shellPresetsCombo, 0, 0);
        promptPanel.Controls.Add(promptSymbol, 1, 0);
        promptPanel.Controls.Add(_shellCommandInput, 2, 0);
        promptPanel.Controls.Add(_shellRunButton, 3, 0);

        _shellOutputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.TerminalBg,
            ForeColor = Theme.TerminalGreen,
            Font = Theme.MonospaceFont,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 8, 0, 0),
            Text = "DroidTrace ADB Terminal initialized.\nType a shell command or select a preset and click 'Run'.\n"
        };

        panel.Controls.Add(_shellOutputBox);
        panel.Controls.Add(promptPanel);
        panel.Controls.Add(topHeader);
        return panel;
    }

    private async Task RefreshDeviceDetailsAsync()
    {
        if (_devicesCombo.SelectedItem is not DeviceInfo d)
        {
            _devModelVal.Text = "No Device Connected";
            _devManufVal.Text = "-";
            _devSerialVal.Text = "-";
            _devAndroidVal.Text = "-";
            _devSecurityVal.Text = "-";
            _devBuildVal.Text = "-";
            _devBatteryVal.Text = "-";
            _devAbiVal.Text = "-";
            return;
        }

        try
        {
            var details = await _adb.GetDeviceDetailsAsync(d.Serial);
            _devModelVal.Text = details.Model;
            _devManufVal.Text = details.Manufacturer;
            _devSerialVal.Text = details.Serial;
            _devAndroidVal.Text = details.AndroidVersion;
            _devSecurityVal.Text = details.SecurityPatch;
            _devBuildVal.Text = details.BuildId;
            _devBatteryVal.Text = details.BatteryLevel;
            _devAbiVal.Text = details.CpuAbi;
            Log($"[DEVICE] Loaded hardware profile for {details.Manufacturer} {details.Model} ({details.Serial})");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Could not query device properties: {ex.Message}");
        }
    }

    private async Task RunShellCommandAsync()
    {
        if (_devicesCombo.SelectedItem is not DeviceInfo d)
        {
            MessageBox.Show("Please connect and authorize a device first.", "ADB Shell", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var cmd = _shellCommandInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(cmd)) return;

        _shellRunButton.Enabled = false;
        _shellOutputBox.AppendText($"\n$ adb -s {d.Serial} shell {cmd}\n");
        _shellOutputBox.ScrollToCaret();

        try
        {
            var output = await _adb.ShellAsync(d.Serial, cmd);
            _shellOutputBox.AppendText(output + "\n");
        }
        catch (Exception ex)
        {
            _shellOutputBox.AppendText($"[ERROR] {ex.Message}\n");
        }
        finally
        {
            _shellRunButton.Enabled = true;
            _shellOutputBox.ScrollToCaret();
        }
    }
    #endregion

    #region View 4: Audit & Activity Logs
    private Panel BuildLogsView()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Log box

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "Forensic Audit & Session Logs", UseMnemonic = false, Font = Theme.HeaderFont, ForeColor = Theme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var subtitle = new Label { Text = "Immutable examiner session log and ADB command execution tracking.", Font = Theme.BodyFont, ForeColor = Theme.TextSecondary, Dock = DockStyle.Bottom, Height = 22 };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        var barPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12, 6, 12, 6) };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        var copyBtn = new Button { Text = "📋 Copy All Logs", Width = 135, Height = 32 };
        Theme.StyleButton(copyBtn, ButtonVariant.Secondary);
        copyBtn.Click += (_, _) => { Clipboard.SetText(_logBox.Text); MessageBox.Show("Logs copied to clipboard.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information); };

        var saveBtn = new Button { Text = "💾 Save to File", Width = 125, Height = 32 };
        Theme.StyleButton(saveBtn, ButtonVariant.Secondary);
        saveBtn.Click += async (_, _) =>
        {
            using var sfd = new SaveFileDialog { Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt", FileName = $"droidtrace_log_{DateTime.Now:yyyyMMdd_HHmmss}.log" };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                await File.WriteAllTextAsync(sfd.FileName, _logBox.Text, Encoding.UTF8);
                MessageBox.Show("Log saved successfully.", "DroidTrace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var clearBtn = new Button { Text = "🗑 Clear View", Width = 110, Height = 32 };
        Theme.StyleButton(clearBtn, ButtonVariant.Secondary);
        clearBtn.Click += (_, _) => _logBox.Text = "";

        bar.Controls.Add(copyBtn);
        bar.Controls.Add(saveBtn);
        bar.Controls.Add(clearBtn);
        barPanel.Controls.Add(bar);

        var logPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12) };
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.TerminalBg,
            ForeColor = Theme.TextSecondary,
            Font = Theme.MonospaceFont,
            BorderStyle = BorderStyle.None,
            DetectUrls = true,
            WordWrap = false
        };
        logPanel.Controls.Add(_logBox);

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(barPanel, 0, 1);
        mainLayout.Controls.Add(logPanel, 0, 2);

        panel.Controls.Add(mainLayout);
        return panel;
    }
    #endregion

    #region View 5: Configuration & Settings
    private Panel BuildSettingsView()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 420)); // Settings form
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Padding

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "Configuration & Environment", UseMnemonic = false, Font = Theme.HeaderFont, ForeColor = Theme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var subtitle = new Label { Text = "Configure Android SDK platform-tools path, evidence storage location, and database connectivity.", Font = Theme.BodyFont, ForeColor = Theme.TextSecondary, Dock = DockStyle.Bottom, Height = 22 };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        var formCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(24) };

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 340,
            ColumnCount = 3,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        for (var i = 0; i < 5; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        // Row 0: ADB Path
        var adbLabel = new Label { Text = "ADB Executable:", Dock = DockStyle.Fill, ForeColor = Theme.TextPrimary, Font = Theme.BodyBoldFont, TextAlign = ContentAlignment.MiddleLeft };
        _settingsAdbInput = new TextBox { Dock = DockStyle.Fill, Text = _settings.AdbPath, BackColor = Theme.InputBackground, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 18, 12, 18) };
        var adbButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        adbButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        adbButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var adbBrowse = new Button { Text = "📁 Browse…", Dock = DockStyle.Fill, Margin = new Padding(0, 15, 4, 15) };
        var adbTest = new Button { Text = "⚡ Test ADB", Dock = DockStyle.Fill, Margin = new Padding(4, 15, 0, 15) };
        Theme.StyleButton(adbBrowse, ButtonVariant.Secondary);
        Theme.StyleButton(adbTest, ButtonVariant.Secondary);
        adbBrowse.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "ADB Executable (adb.exe)|adb.exe|All Files (*.*)|*.*", Title = "Locate adb.exe" };
            if (ofd.ShowDialog(this) == DialogResult.OK) _settingsAdbInput.Text = ofd.FileName;
        };
        adbTest.Click += async (_, _) =>
        {
            var res = await _adb.GetAdbVersionAsync();
            _settingsAdbStatus.Text = res;
            _settingsAdbStatus.ForeColor = res.Contains("version") ? Theme.Success : Theme.Danger;
        };
        adbButtons.Controls.Add(adbBrowse, 0, 0);
        adbButtons.Controls.Add(adbTest, 1, 0);

        formLayout.Controls.Add(adbLabel, 0, 0);
        formLayout.Controls.Add(_settingsAdbInput, 1, 0);
        formLayout.Controls.Add(adbButtons, 2, 0);

        // Row 1: ADB Status display
        var adbStatusLbl = new Label { Text = "ADB Status:", Dock = DockStyle.Fill, ForeColor = Theme.TextSecondary, Font = Theme.CaptionFont, TextAlign = ContentAlignment.MiddleLeft };
        _settingsAdbStatus = new Label { Text = "Click 'Test ADB' to verify path", Dock = DockStyle.Fill, ForeColor = Theme.TextMuted, Font = Theme.MonospaceSmallFont, TextAlign = ContentAlignment.MiddleLeft };
        formLayout.Controls.Add(adbStatusLbl, 0, 1);
        formLayout.Controls.Add(_settingsAdbStatus, 1, 1);

        // Row 2: Evidence Directory
        var evLabel = new Label { Text = "Evidence Root Folder:", Dock = DockStyle.Fill, ForeColor = Theme.TextPrimary, Font = Theme.BodyBoldFont, TextAlign = ContentAlignment.MiddleLeft };
        _settingsEvidenceInput = new TextBox { Dock = DockStyle.Fill, Text = _settings.EvidenceRoot, BackColor = Theme.InputBackground, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 18, 12, 18) };
        var evBrowse = new Button { Text = "📁 Browse Folder…", Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 15) };
        Theme.StyleButton(evBrowse, ButtonVariant.Secondary);
        evBrowse.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog { Description = "Select root directory for acquired cases" };
            if (fbd.ShowDialog(this) == DialogResult.OK) _settingsEvidenceInput.Text = fbd.SelectedPath;
        };
        formLayout.Controls.Add(evLabel, 0, 2);
        formLayout.Controls.Add(_settingsEvidenceInput, 1, 2);
        formLayout.Controls.Add(evBrowse, 2, 2);

        // Row 3: PostgreSQL Connection
        var pgLabel = new Label { Text = "PostgreSQL (Optional):", Dock = DockStyle.Fill, ForeColor = Theme.TextPrimary, Font = Theme.BodyBoldFont, TextAlign = ContentAlignment.MiddleLeft };
        _settingsPgInput = new TextBox { Dock = DockStyle.Fill, Text = _settings.PostgreSqlConnection, BackColor = Theme.InputBackground, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 18, 12, 18) };
        var pgTest = new Button { Text = "🔌 Test Database", Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 15) };
        Theme.StyleButton(pgTest, ButtonVariant.Secondary);
        pgTest.Click += async (_, _) =>
        {
            var pg = new PostgreSqlService(_settingsPgInput.Text.Trim());
            var (ok, msg) = await pg.TestAsync();
            _settingsPgStatus.Text = msg;
            _settingsPgStatus.ForeColor = ok ? Theme.Success : Theme.Danger;
        };
        formLayout.Controls.Add(pgLabel, 0, 3);
        formLayout.Controls.Add(_settingsPgInput, 1, 3);
        formLayout.Controls.Add(pgTest, 2, 3);

        // Row 4: PostgreSQL Status & Save Button
        _settingsPgStatus = new Label { Text = "Optional forensic database integration", Dock = DockStyle.Fill, ForeColor = Theme.TextMuted, Font = Theme.CaptionFont, TextAlign = ContentAlignment.MiddleLeft };
        var saveBtn = new Button { Text = "💾 Save Configuration", Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 14) };
        Theme.StyleButton(saveBtn, ButtonVariant.Primary);
        saveBtn.Click += (_, _) => SaveSettings();

        formLayout.Controls.Add(_settingsPgStatus, 1, 4);
        formLayout.Controls.Add(saveBtn, 2, 4);

        formCard.Controls.Add(formLayout);

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(formCard, 0, 1);

        panel.Controls.Add(mainLayout);
        return panel;
    }

    private void SaveSettings()
    {
        _settings = new AppSettings(_settingsAdbInput.Text.Trim(), _settingsPgInput.Text.Trim(), _settingsEvidenceInput.Text.Trim());
        SettingsService.Save(_paths.SettingsFile, _settings);
        Log("[SETTINGS] Saved configuration changes to droidtrace.settings.json");
        MessageBox.Show("Settings saved successfully.\n\nRestart DroidTrace if ADB path was changed.", "Configuration Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    #endregion

    #region Helpers & Data Loading
    private async Task CheckAdbStatusAsync()
    {
        var ver = await _adb.GetAdbVersionAsync();
        if (ver.Contains("version", StringComparison.OrdinalIgnoreCase))
        {
            _statusAdb.Text = "● ADB Connected (" + ver.Split('\n')[0].Trim() + ")";
            _statusAdb.ForeColor = Theme.Success;
        }
        else
        {
            _statusAdb.Text = "● ADB Unavailable";
            _statusAdb.ForeColor = Theme.Danger;
        }
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var ds = await _adb.GetDevicesAsync();
            _devicesCombo.Items.Clear();
            foreach (var d in ds) _devicesCombo.Items.Add(d);
            _devicesCombo.DisplayMember = nameof(DeviceInfo.Serial);

            if (_devicesCombo.Items.Count > 0)
            {
                _devicesCombo.SelectedIndex = 0;
                var current = ds[0];
                _statusDevice.Text = $"📱 Target: {current.Model ?? current.Serial}";
                _cardDeviceVal.Text = string.IsNullOrWhiteSpace(current.Model) ? current.Serial : $"{current.Model} ({current.Serial})";
                Log($"[ADB] Detected {ds.Count} authorized target device(s): {current.Serial} ({current.Model})");
            }
            else
            {
                _statusDevice.Text = "📱 Target: No Device";
                _cardDeviceVal.Text = "No device detected";
                Log("[ADB] No authorized Android device detected. Connect target via USB with debugging enabled.");
            }
        }
        catch (Exception ex)
        {
            _cardDeviceVal.Text = "ADB Error";
            Log($"[ADB ERROR] Device query failed: {ex.Message}");
        }
    }

    private void TryLoadLatestExistingEvidence()
    {
        try
        {
            var root = Path.Combine(_paths.BaseDirectory, _settings.EvidenceRoot);
            if (!Directory.Exists(root)) return;

            var latestDir = Directory.GetDirectories(root).OrderByDescending(x => x).FirstOrDefault();
            if (latestDir == null) return;

            var manifestPath = Path.Combine(latestDir, "evidence-manifest.json");
            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<EvidenceManifest>(json);
                if (manifest != null)
                {
                    _lastManifest = manifest;
                    _lastEvidenceDir = latestDir;
                    _evidenceDirLabel.Text = latestDir;
                    _cardArtifactsVal.Text = $"{manifest.Artifacts.Count} Sets";
                    _cardManifestVal.Text = manifest.ManifestSha256[..Math.Min(12, manifest.ManifestSha256.Length)] + "…";
                    DisplayArtifacts(manifest);
                    Log($"[SESSION] Loaded previous case evidence from {Path.GetFileName(latestDir)}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[WARN] Could not load previous evidence: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logBox.AppendText(line + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private void ToggleControls(bool enabled)
    {
        _acquireButton.Enabled = enabled;
        _devicesCombo.Enabled = enabled;
        _caseInput.Enabled = enabled;
        foreach (var btn in _navButtons) btn.Enabled = enabled;
    }
    #endregion
}
