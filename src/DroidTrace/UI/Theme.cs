namespace DroidTrace.UI;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Success,
    Danger,
    Ghost
}

public static class Theme
{
    public static readonly Color Background = Color.FromArgb(15, 23, 42); // #0f172a
    public static readonly Color Surface = Color.FromArgb(30, 41, 59); // #1e293b
    public static readonly Color SurfaceLight = Color.FromArgb(41, 53, 72); // #293548
    public static readonly Color Border = Color.FromArgb(51, 65, 85); // #334155
    public static readonly Color BorderLight = Color.FromArgb(71, 85, 105); // #475569

    public static readonly Color SidebarBackground = Color.FromArgb(10, 15, 29); // #0a0f1d
    public static readonly Color SidebarHover = Color.FromArgb(24, 33, 47);
    public static readonly Color SidebarActive = Color.FromArgb(30, 41, 59);

    public static readonly Color Primary = Color.FromArgb(37, 99, 235); // #2563eb
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216); // #1d4ed8
    public static readonly Color PrimaryLight = Color.FromArgb(59, 130, 246);

    public static readonly Color Success = Color.FromArgb(16, 185, 129); // #10b981
    public static readonly Color SuccessHover = Color.FromArgb(5, 150, 105);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11); // #f59e0b
    public static readonly Color Danger = Color.FromArgb(239, 68, 68); // #ef4444
    public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);

    public static readonly Color Cyan = Color.FromArgb(6, 182, 212); // #06b6d4
    public static readonly Color Purple = Color.FromArgb(139, 92, 246); // #8b5cf6

    public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252); // #f8fafc
    public static readonly Color TextSecondary = Color.FromArgb(148, 163, 184); // #94a3b8
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139); // #64748b

    public static readonly Color InputBackground = Color.FromArgb(15, 23, 42);
    public static readonly Color InputBorder = Color.FromArgb(51, 65, 85);

    public static readonly Color GridRowEven = Color.FromArgb(24, 33, 47);
    public static readonly Color GridRowOdd = Color.FromArgb(30, 41, 59);

    public static readonly Color TerminalBg = Color.FromArgb(8, 12, 20);
    public static readonly Color TerminalGreen = Color.FromArgb(74, 222, 128);
    public static readonly Color TerminalCyan = Color.FromArgb(56, 189, 248);

    public static readonly Font HeaderFont = new("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font SubHeaderFont = new("Segoe UI", 12F, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI", 10.5F, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5F);
    public static readonly Font BodyBoldFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font CaptionFont = new("Segoe UI", 8.5F);
    public static readonly Font CaptionBoldFont = new("Segoe UI", 8.5F, FontStyle.Bold);
    public static readonly Font MonospaceFont = new("Consolas", 9.5F);
    public static readonly Font MonospaceSmallFont = new("Consolas", 8.5F);

    public static void StyleButton(Button btn, ButtonVariant variant = ButtonVariant.Secondary)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.UseVisualStyleBackColor = false;
        btn.Cursor = Cursors.Hand;
        btn.Font = BodyBoldFont;

        switch (variant)
        {
            case ButtonVariant.Primary:
                btn.BackColor = Primary;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = PrimaryLight;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = PrimaryHover;
                break;
            case ButtonVariant.Success:
                btn.BackColor = Success;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(52, 211, 153);
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = SuccessHover;
                break;
            case ButtonVariant.Danger:
                btn.BackColor = Danger;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(248, 113, 113);
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = DangerHover;
                break;
            case ButtonVariant.Ghost:
                btn.BackColor = Color.Transparent;
                btn.ForeColor = TextSecondary;
                btn.FlatAppearance.BorderColor = Border;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = SurfaceLight;
                break;
            case ButtonVariant.Secondary:
            default:
                btn.BackColor = Surface;
                btn.ForeColor = TextPrimary;
                btn.FlatAppearance.BorderColor = Border;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = SurfaceLight;
                break;
        }
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Background;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoGenerateColumns = false;
        grid.RowTemplate.Height = 34;
        grid.ColumnHeadersHeight = 36;
        grid.ScrollBars = ScrollBars.Both;

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SurfaceLight,
            ForeColor = TextSecondary,
            Font = TitleFont,
            Padding = new Padding(8, 0, 8, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridRowOdd,
            ForeColor = TextPrimary,
            SelectionBackColor = Primary,
            SelectionForeColor = Color.White,
            Font = BodyFont,
            Padding = new Padding(8, 0, 8, 0),
            WrapMode = DataGridViewTriState.False
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridRowEven,
            ForeColor = TextPrimary,
            SelectionBackColor = Primary,
            SelectionForeColor = Color.White,
            Font = BodyFont,
            Padding = new Padding(8, 0, 8, 0),
            WrapMode = DataGridViewTriState.False
        };
    }

    public static Panel CreateCard(string title, string value, out Label valueLabel, Color? accentColor = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(4),
            Padding = new Padding(14, 10, 14, 10)
        };

        // Top accent line if provided
        if (accentColor.HasValue)
        {
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = accentColor.Value
            };
            card.Controls.Add(topBar);
        }

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = TextSecondary,
            Font = CaptionBoldFont,
            AutoEllipsis = true
        };

        valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(valueLabel);
        card.Controls.Add(lblTitle);
        return card;
    }
}
