using System.Drawing.Drawing2D;
using MLBB.Core.Entities;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Controls;

public class PickSlotControl : Control
{
    private Hero? _hero;
    private bool _isSelected;
    private bool _isMyHero;
    private string _lane = "Mid";
    private string _spell = "flicker";
    private readonly Item?[] _items = new Item?[6];
    private readonly bool _isEnemy;
    private readonly int _slotIndex;

    // Controls
    private readonly PictureBox _picAvatar;
    private readonly Label _lblName;
    private readonly Label _lblRole;
    private readonly PictureBox _picLane;
    private readonly PictureBox _picSpell;
    private readonly PictureBox[] _picItems = new PictureBox[6];
    private readonly Button _btnClear;
    private readonly Label _lblMyHeroBadge;

    public int SlotIndex => _slotIndex;
    public bool IsEnemy => _isEnemy;

    public Hero? Hero
    {
        get => _hero;
        set
        {
            _hero = value;
            UpdateDisplay();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Invalidate(); }
    }

    public bool IsMyHero
    {
        get => _isMyHero;
        set
        {
            _isMyHero = value;
            _lblMyHeroBadge.Visible = _isMyHero && !_isEnemy;
            Invalidate();
        }
    }

    public string Lane
    {
        get => _lane;
        set { _lane = value; _picLane.Image = ImageCache.GetLaneImage(_lane, 22, 22); }
    }

    public string Spell
    {
        get => _spell;
        set { _spell = value; _picSpell.Image = ImageCache.GetSpellImage(_spell, 24, 24); }
    }

    public event Action<int, bool>? HeroAvatarClicked;
    public event Action<int, bool>? LaneClicked;
    public event Action<int, bool>? SpellClicked;
    public event Action<int, int, bool>? ItemSlotClicked;
    public event Action<int, bool>? ClearClicked;
    public event Action<int, bool>? CardSelected;

    private static readonly Color BgCard = Color.FromArgb(24, 34, 53);           // #182235
    private static readonly Color BgCardActive = Color.FromArgb(30, 41, 65);       // #1e2941
    private static readonly Color BorderDefault = Color.FromArgb(51, 65, 85);       // #334155
    private static readonly Color CyanBorder = Color.FromArgb(6, 182, 212);        // #06b6d4
    private static readonly Color CrimsonBorder = Color.FromArgb(225, 29, 72);      // #e11d48
    private static readonly Color GoldAccent = Color.FromArgb(245, 158, 11);

    public PickSlotControl(int slotIndex, bool isEnemy)
    {
        _slotIndex = slotIndex;
        _isEnemy = isEnemy;

        Size = new Size(420, 94);
        MinimumSize = new Size(360, 94);
        DoubleBuffered = true;
        BackColor = BgCard;
        Cursor = Cursors.Hand;

        Click += (_, _) => CardSelected?.Invoke(_slotIndex, _isEnemy);

        // 1. Hero Avatar Box (Circular)
        _picAvatar = new PictureBox
        {
            Size = new Size(62, 62),
            Location = new Point(12, 16),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _picAvatar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, _picAvatar.Width - 3, _picAvatar.Height - 3);

            if (_hero == null)
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                g.FillEllipse(bgBrush, bounds);

                using var dashPen = new Pen(_isEnemy ? Color.FromArgb(140, 225, 29, 72) : Color.FromArgb(140, 6, 182, 212), 1.5f)
                {
                    DashStyle = DashStyle.Dash
                };
                g.DrawEllipse(dashPen, bounds);

                using var brush = new SolidBrush(Color.FromArgb(100, 116, 139));
                using var font = new Font("Segoe UI", 13f, FontStyle.Bold);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("+", font, brush, bounds, sf);
            }
            else
            {
                var ringColor = _isEnemy ? CrimsonBorder : CyanBorder;
                using var borderPen = new Pen(ringColor, 2f);
                g.DrawEllipse(borderPen, bounds);
            }
        };
        _picAvatar.Click += (_, _) => HeroAvatarClicked?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_picAvatar);

        // 2. Hero Name Label
        _lblName = new Label
        {
            Text = $"Empty Slot {_slotIndex + 1}",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(82, 14),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _lblName.Click += (_, _) => CardSelected?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_lblName);

        // 3. Hero Role Label
        _lblRole = new Label
        {
            Text = "Click to select hero",
            Font = new Font("Segoe UI", 7.8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(82, 34),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _lblRole.Click += (_, _) => CardSelected?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_lblRole);

        // 4. Lane Badge Button (Circular)
        _picLane = new PictureBox
        {
            Size = new Size(26, 26),
            Location = new Point(82, 54),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _picLane.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, _picLane.Width - 1, _picLane.Height - 1);
            using var pen = new Pen(Color.FromArgb(71, 85, 105), 1.2f);
            g.DrawEllipse(pen, bounds);
        };
        _picLane.Click += (_, _) => LaneClicked?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_picLane);

        // 5. Spell Badge Button (Circular)
        _picSpell = new PictureBox
        {
            Size = new Size(26, 26),
            Location = new Point(114, 54),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _picSpell.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, _picSpell.Width - 1, _picSpell.Height - 1);
            using var pen = new Pen(Color.FromArgb(71, 85, 105), 1.2f);
            g.DrawEllipse(pen, bounds);
        };
        _picSpell.Click += (_, _) => SpellClicked?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_picSpell);

        // 6. 6 Equipment Items (Circular Placeholders)
        int itemStartX = 152;
        for (int i = 0; i < 6; i++)
        {
            int itemIndex = i;
            var picItem = new PictureBox
            {
                Size = new Size(26, 26),
                Location = new Point(itemStartX + (i * 31), 54),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            picItem.Click += (_, _) => ItemSlotClicked?.Invoke(_slotIndex, itemIndex, _isEnemy);
            picItem.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new Rectangle(0, 0, picItem.Width - 1, picItem.Height - 1);

                if (_items[itemIndex] == null)
                {
                    using var fillBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                    g.FillEllipse(fillBrush, bounds);

                    using var dashPen = new Pen(Color.FromArgb(51, 65, 85), 1f)
                    {
                        DashStyle = DashStyle.Dash
                    };
                    g.DrawEllipse(dashPen, bounds);

                    using var dotBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                    g.FillEllipse(dotBrush, picItem.Width / 2 - 2, picItem.Height / 2 - 2, 4, 4);
                }
                else
                {
                    using var ringPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f);
                    g.DrawEllipse(ringPen, bounds);
                }
            };
            _picItems[i] = picItem;
            Controls.Add(picItem);
        }

        // 7. My Hero Badge
        _lblMyHeroBadge = new Label
        {
            Text = "★ MY HERO",
            Font = new Font("Segoe UI", 7.2f, FontStyle.Bold),
            ForeColor = GoldAccent,
            BackColor = Color.FromArgb(40, 245, 158, 11),
            Padding = new Padding(3, 1, 3, 1),
            AutoSize = true,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _lblMyHeroBadge.Location = new Point(Width - 110, 10);
        Controls.Add(_lblMyHeroBadge);

        // 8. Clear Button
        _btnClear = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(22, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.Location = new Point(Width - 30, 8);
        _btnClear.Click += (_, _) => ClearClicked?.Invoke(_slotIndex, _isEnemy);
        Controls.Add(_btnClear);

        UpdateDisplay();
    }

    public void SetItem(int itemIndex, Item? item)
    {
        if (itemIndex >= 0 && itemIndex < 6)
        {
            _items[itemIndex] = item;
            _picItems[itemIndex].Image = ImageCache.GetItemImage(item?.Id, 26, 26, circular: true);
            _picItems[itemIndex].Invalidate();
        }
    }

    private void UpdateDisplay()
    {
        if (_hero != null)
        {
            _picAvatar.Image = ImageCache.GetHeroImage(_hero.Id, 62, 62, circular: true);
            _lblName.Text = _hero.Name;
            _lblName.ForeColor = Color.FromArgb(248, 250, 252);
            _lblRole.Text = _hero.Role ?? "Hero";
            _lblRole.ForeColor = _isEnemy ? Color.FromArgb(244, 63, 94) : Color.FromArgb(56, 189, 248);
            _btnClear.Visible = true;
        }
        else
        {
            _picAvatar.Image = null;
            _lblName.Text = $"Slot {_slotIndex + 1} (Empty)";
            _lblName.ForeColor = Color.FromArgb(148, 163, 184);
            _lblRole.Text = "Click to assign hero";
            _lblRole.ForeColor = Color.FromArgb(100, 116, 139);
            _btnClear.Visible = false;
        }

        _picLane.Image = ImageCache.GetLaneImage(_lane, 26, 26, circular: true);
        _picSpell.Image = ImageCache.GetSpellImage(_spell, 26, 26, circular: true);
        _picAvatar.Invalidate();
        _picLane.Invalidate();
        _picSpell.Invalidate();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_btnClear != null) _btnClear.Location = new Point(Width - 28, 8);
        if (_lblMyHeroBadge != null) _lblMyHeroBadge.Location = new Point(Width - 110, 10);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = ImageCache.GetRoundedRectPath(rect, 6))
        {
            using var fillBrush = new SolidBrush(_isSelected ? BgCardActive : BgCard);
            g.FillPath(fillBrush, path);

            var borderColor = _isSelected
                ? (_isEnemy ? CrimsonBorder : CyanBorder)
                : BorderDefault;

            using var pen = new Pen(borderColor, _isSelected ? 2f : 1f);
            g.DrawPath(pen, path);
        }
    }
}
