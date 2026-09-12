using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Dialogs;

public class ItemSelectDialog : Form
{
    private readonly IHeroDataService _heroDataService;
    private readonly FlowLayoutPanel _grid;
    private readonly TextBox _searchBox;
    private readonly List<Button> _filterButtons = new();
    private string _activeCategory = "All";
    private readonly List<Item> _allItems;

    public Item? SelectedItem { get; private set; }
    public bool RemoveRequested { get; private set; } = false;

    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);
    private static readonly Color BgCard = Color.FromArgb(20, 29, 47);
    private static readonly Color BgCardHover = Color.FromArgb(30, 45, 70);
    private static readonly Color BorderColor = Color.FromArgb(40, 53, 76);
    private static readonly Color GoldAccent = Color.FromArgb(245, 158, 11);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public ItemSelectDialog(IHeroDataService heroDataService)
    {
        _heroDataService = heroDataService;
        _allItems = _heroDataService.GetAllItems().OrderBy(i => i.Name).ToList();

        Text = "SELECT EQUIPMENT";
        Size = new Size(1180, 740);
        MinimumSize = new Size(1080, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        DoubleBuffered = true;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        // Header Panel (NO remove item button per requirement)
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = BgDark,
            Padding = new Padding(20, 12, 20, 8)
        };
        Controls.Add(header);

        var lblTitle = new Label
        {
            Text = "SELECT EQUIPMENT",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = GoldAccent,
            AutoSize = true,
            Location = new Point(20, 12)
        };
        header.Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = "Browse all MLBB offensive, defensive, magic, movement, and roaming equipment.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(220, 16)
        };
        header.Controls.Add(lblSub);

        // Search Box
        _searchBox = new TextBox
        {
            PlaceholderText = "🔍 Search item name...",
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(240, 28),
            Location = new Point(20, 48)
        };
        _searchBox.TextChanged += (_, _) => PopulateGrid();
        header.Controls.Add(_searchBox);

        // Category Filter Bar
        var filterBar = new FlowLayoutPanel
        {
            Location = new Point(280, 46),
            Size = new Size(Width - 300, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        header.Controls.Add(filterBar);

        string[] categories = ["All", "Attack", "Magic", "Defense", "Movement", "Roam"];
        foreach (var cat in categories)
        {
            var btnCat = new Button
            {
                Text = cat.ToUpperInvariant(),
                Tag = cat,
                BackColor = cat == "All" ? GoldAccent : BgCard,
                ForeColor = cat == "All" ? Color.Black : TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Height = 28,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnCat.FlatAppearance.BorderSize = 1;
            btnCat.FlatAppearance.BorderColor = cat == "All" ? GoldAccent : BorderColor;

            btnCat.Click += (_, _) =>
            {
                _activeCategory = cat;
                foreach (var b in _filterButtons)
                {
                    bool isActive = (string)b.Tag! == _activeCategory;
                    b.BackColor = isActive ? GoldAccent : BgCard;
                    b.ForeColor = isActive ? Color.Black : TextMuted;
                    b.FlatAppearance.BorderColor = isActive ? GoldAccent : BorderColor;
                }
                PopulateGrid();
            };
            _filterButtons.Add(btnCat);
            filterBar.Controls.Add(btnCat);
        }

        // Grid of Item Cards - Sized so scroll bar is not needed
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
        var filtered = _allItems.Where(it =>
        {
            if (_activeCategory != "All")
            {
                if (string.IsNullOrEmpty(it.Type) || !it.Type.Contains(_activeCategory, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(query))
            {
                return (it.Name ?? string.Empty).ToLowerInvariant().Contains(query) ||
                       (it.Id ?? string.Empty).ToLowerInvariant().Contains(query);
            }
            return true;
        }).ToList();

        foreach (var item in filtered)
        {
            _grid.Controls.Add(CreateItemCard(item));
        }

        _grid.ResumeLayout(true);
    }

    private Control CreateItemCard(Item item)
    {
        var panel = new Panel
        {
            Size = new Size(100, 96),
            Margin = new Padding(6),
            BackColor = BgCard,
            Cursor = Cursors.Hand
        };

        var img = ImageCache.GetItemImage(item.Id, 48, 48);
        var pic = new PictureBox
        {
            Size = new Size(48, 48),
            Location = new Point(26, 8),
            Image = img,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        var lblName = new Label
        {
            Text = item.Name,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            TextAlign = ContentAlignment.TopCenter,
            Size = new Size(96, 32),
            Location = new Point(2, 60),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        void OnSelect(object? sender, EventArgs e)
        {
            SelectedItem = item;
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

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        panel.Controls.Add(pic);
        panel.Controls.Add(lblName);
        return panel;
    }
}
