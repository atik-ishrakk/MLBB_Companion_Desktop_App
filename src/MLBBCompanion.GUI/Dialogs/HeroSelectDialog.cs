using System.Drawing.Drawing2D;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Dialogs;

public class HeroSelectDialog : Form
{
    private readonly IHeroDataService _heroDataService;
    private readonly FlowLayoutPanel _grid;
    private readonly TextBox _searchBox;
    private readonly List<Button> _filterButtons = new();
    private string _activeRole = "All";
    private readonly List<Hero> _allHeroes;
    private readonly HashSet<string> _unavailableHeroIds;

    public Hero? SelectedHero { get; private set; }
    public bool ClearRequested { get; private set; }

    private static readonly Color BgDark = Color.FromArgb(15, 23, 42);       // #0f172a
    private static readonly Color BgCard = Color.FromArgb(30, 41, 59);       // #1e293b
    private static readonly Color BgCardHover = Color.FromArgb(51, 65, 85);  // #334155
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);  // #06b6d4
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public HeroSelectDialog(IHeroDataService heroDataService, string title = "SELECT HERO", HashSet<string>? unavailableHeroIds = null)
    {
        _heroDataService = heroDataService;
        _allHeroes = _heroDataService.GetAllHeroes().OrderBy(h => h.Name).ToList();
        _unavailableHeroIds = unavailableHeroIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Text = title;
        Size = new Size(1180, 740);
        MinimumSize = new Size(1080, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        // Header Panel
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackColor = BgDark,
            Padding = new Padding(20, 14, 20, 10)
        };
        Controls.Add(header);

        var lblTitle = new Label
        {
            Text = title.ToUpperInvariant(),
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = CyanAccent,
            AutoSize = true,
            Location = new Point(20, 12)
        };
        header.Controls.Add(lblTitle);

        var btnClear = new Button
        {
            Text = "✕ Clear Hero",
            BackColor = Color.FromArgb(225, 29, 72),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Size = new Size(110, 28),
            Location = new Point(Width - 150, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnClear.FlatAppearance.BorderSize = 0;
        btnClear.Click += (_, _) =>
        {
            ClearRequested = true;
            SelectedHero = null;
            DialogResult = DialogResult.OK;
            Close();
        };
        header.Controls.Add(btnClear);

        // Search Box
        _searchBox = new TextBox
        {
            PlaceholderText = "Search hero by name...",
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
            Size = new Size(240, 28),
            Location = new Point(20, 56)
        };
        _searchBox.TextChanged += (_, _) => PopulateGrid();
        header.Controls.Add(_searchBox);

        // Filter Bar
        var filterBar = new FlowLayoutPanel
        {
            Location = new Point(280, 54),
            Size = new Size(Width - 300, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        header.Controls.Add(filterBar);

        string[] roles = ["All", "Tank", "Fighter", "Assassin", "Mage", "Marksman", "Support"];
        foreach (var role in roles)
        {
            var btnRole = new Button
            {
                Text = role.ToUpperInvariant(),
                Tag = role,
                BackColor = role == "All" ? CyanAccent : BgCard,
                ForeColor = role == "All" ? Color.Black : TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Height = 28,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            btnRole.FlatAppearance.BorderSize = 0;
            btnRole.Click += (_, _) =>
            {
                _activeRole = role;
                foreach (var b in _filterButtons)
                {
                    bool isActive = (string)b.Tag! == _activeRole;
                    b.BackColor = isActive ? CyanAccent : BgCard;
                    b.ForeColor = isActive ? Color.Black : TextMuted;
                }
                PopulateGrid();
            };
            _filterButtons.Add(btnRole);
            filterBar.Controls.Add(btnRole);
        }

        // Hero Grid
        _grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(10, 15, 29),
            Padding = new Padding(20, 10, 20, 20)
        };
        Controls.Add(_grid);
        _grid.BringToFront();

        PopulateGrid();
    }

    private void PopulateGrid()
    {
        _grid.SuspendLayout();
        _grid.Controls.Clear();

        var query = _searchBox.Text.Trim().ToLowerInvariant();
        var filtered = _allHeroes.Where(h =>
        {
            if (_activeRole != "All")
            {
                if (string.IsNullOrEmpty(h.Role) || !h.Role.Contains(_activeRole, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(query))
            {
                return (h.Name ?? string.Empty).ToLowerInvariant().Contains(query) ||
                       (h.Id ?? string.Empty).ToLowerInvariant().Contains(query);
            }
            return true;
        }).ToList();

        foreach (var hero in filtered)
        {
            var card = CreateHeroCard(hero);
            _grid.Controls.Add(card);
        }

        _grid.ResumeLayout(true);
    }

    private Control CreateHeroCard(Hero hero)
    {
        bool isUnavailable = _unavailableHeroIds.Contains(hero.Id);

        var panel = new Panel
        {
            Size = new Size(110, 110),
            Margin = new Padding(6),
            BackColor = isUnavailable ? Color.FromArgb(16, 22, 34) : BgCard,
            Cursor = isUnavailable ? Cursors.No : Cursors.Hand
        };

        var imgHero = ImageCache.GetHeroImage(hero.Id, 64, 64, circular: false);
        var pic = new PictureBox
        {
            Size = new Size(64, 64),
            Location = new Point(23, 10),
            Image = imgHero,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = isUnavailable ? Cursors.No : Cursors.Hand
        };

        var lblName = new Label
        {
            Text = hero.Name,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = isUnavailable ? Color.FromArgb(90, 105, 130) : TextPrimary,
            TextAlign = ContentAlignment.TopCenter,
            Size = new Size(106, 30),
            Location = new Point(2, 78),
            BackColor = Color.Transparent,
            Cursor = isUnavailable ? Cursors.No : Cursors.Hand
        };

        panel.Controls.Add(pic);
        panel.Controls.Add(lblName);

        if (isUnavailable)
        {
            var lblTaken = new Label
            {
                Text = "TAKEN",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 63, 94),
                BackColor = Color.FromArgb(200, 15, 23, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(64, 20),
                Location = new Point(23, 34),
                Cursor = Cursors.No
            };
            lblTaken.Paint += (_, e) =>
            {
                using var p = new Pen(Color.FromArgb(244, 63, 94), 1);
                e.Graphics.DrawRectangle(p, 0, 0, lblTaken.Width - 1, lblTaken.Height - 1);
            };
            panel.Controls.Add(lblTaken);
            lblTaken.BringToFront();
        }
        else
        {
            void OnSelect(object? sender, EventArgs e)
            {
                SelectedHero = hero;
                DialogResult = DialogResult.OK;
                Close();
            }

            panel.Click += OnSelect;
            pic.Click += OnSelect;
            lblName.Click += OnSelect;

            panel.MouseEnter += (_, _) => { panel.BackColor = BgCardHover; };
            panel.MouseLeave += (_, _) => { panel.BackColor = BgCard; };
            pic.MouseEnter += (_, _) => { panel.BackColor = BgCardHover; };
            lblName.MouseEnter += (_, _) => { panel.BackColor = BgCardHover; };
        }

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(isUnavailable ? Color.FromArgb(50, 25, 35) : BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }
}
