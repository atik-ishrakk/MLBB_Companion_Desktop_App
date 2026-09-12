using System.Drawing.Drawing2D;
using MLBB.Core.Entities;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Controls;

public class BanSlotControl : Control
{
    private Hero? _hero;
    private bool _isHovered;
    private readonly bool _isEnemy;
    private readonly int _slotIndex;

    public Hero? Hero
    {
        get => _hero;
        set { _hero = value; Invalidate(); }
    }

    public event Action<int, bool>? BanSlotClicked;

    private static readonly Color BgDark = Color.FromArgb(16, 24, 40);
    private static readonly Color BorderDefault = Color.FromArgb(51, 65, 85);
    private static readonly Color AllyBorder = Color.FromArgb(6, 182, 212);
    private static readonly Color EnemyBorder = Color.FromArgb(225, 29, 72);

    public BanSlotControl(int slotIndex, bool isEnemy)
    {
        _slotIndex = slotIndex;
        _isEnemy = isEnemy;

        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        Size = new Size(50, 50);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;

        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        Click += (_, _) => BanSlotClicked?.Invoke(_slotIndex, _isEnemy);

        UpdateRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        try
        {
            if (Width > 0 && Height > 0)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, Width, Height);
                this.Region = new Region(path);
            }
        }
        catch { }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Suppress default background erase to prevent square box flicker
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(2, 2, Width - 5, Height - 5);

        // Draw Hero Image if present
        if (_hero != null)
        {
            // Background Circle for hero
            using (var brush = new SolidBrush(BgDark))
            {
                g.FillEllipse(brush, bounds);
            }

            var img = ImageCache.GetHeroImage(_hero.Id, bounds.Width, bounds.Height, circular: true);
            if (img != null)
            {
                g.DrawImage(img, bounds);
            }
            else
            {
                var initial = !string.IsNullOrEmpty(_hero.Name) ? _hero.Name[..1].ToUpperInvariant() : "?";
                using var textBrush = new SolidBrush(Color.White);
                using var font = new Font("Segoe UI", 12f, FontStyle.Bold);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(initial, font, textBrush, bounds, sf);
            }

            // Clean esports circular border ring (NO red cross or slash over hero face)
            var ringColor = _isHovered ? Color.White : (_isEnemy ? EnemyBorder : AllyBorder);
            using var borderPen = new Pen(ringColor, _isHovered ? 2.5f : 2f);
            g.DrawEllipse(borderPen, bounds);
        }
        else
        {
            // Empty placeholder slot: purely round and transparent
            using (var brush = new SolidBrush(Color.FromArgb(35, 15, 23, 42)))
            {
                g.FillEllipse(brush, bounds);
            }

            // Circular dashed placeholder outline
            var dashColor = _isEnemy ? Color.FromArgb(170, 225, 29, 72) : Color.FromArgb(170, 6, 182, 212);
            using var dashPen = new Pen(dashColor, 1.5f)
            {
                DashStyle = DashStyle.Dash
            };
            g.DrawEllipse(dashPen, bounds);

            // Centered slot number
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var brushText = new SolidBrush(Color.FromArgb(148, 163, 184));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString($"B{_slotIndex + 1}", font, brushText, bounds, sf);

            // Outer Ring Border
            var ringColor = _isHovered ? Color.White : BorderDefault;
            using var borderPen = new Pen(ringColor, _isHovered ? 2f : 1.2f);
            g.DrawEllipse(borderPen, bounds);
        }
    }
}
