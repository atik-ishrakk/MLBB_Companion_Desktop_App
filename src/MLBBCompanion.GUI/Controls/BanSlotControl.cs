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

    private static readonly Color BgDark = Color.FromArgb(20, 29, 47);
    private static readonly Color BorderDefault = Color.FromArgb(71, 85, 105);
    private static readonly Color AllyBorder = Color.FromArgb(6, 182, 212);
    private static readonly Color EnemyBorder = Color.FromArgb(225, 29, 72);

    public BanSlotControl(int slotIndex, bool isEnemy)
    {
        _slotIndex = slotIndex;
        _isEnemy = isEnemy;

        Size = new Size(52, 52);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;

        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        Click += (_, _) => BanSlotClicked?.Invoke(_slotIndex, _isEnemy);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(2, 2, Width - 5, Height - 5);

        // Background Circle
        using (var brush = new SolidBrush(BgDark))
        {
            g.FillEllipse(brush, bounds);
        }

        // Draw Hero Image if present
        if (_hero != null)
        {
            var img = ImageCache.GetHeroImage(_hero.Id, bounds.Width, bounds.Height, circular: true);
            if (img != null)
            {
                g.DrawImage(img, bounds);
            }
            else
            {
                // Fallback initial
                var initial = !string.IsNullOrEmpty(_hero.Name) ? _hero.Name[..1].ToUpperInvariant() : "?";
                using var textBrush = new SolidBrush(Color.White);
                using var font = new Font("Segoe UI", 12f, FontStyle.Bold);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(initial, font, textBrush, bounds, sf);
            }

            // Red Ban Slash overlay
            using var slashPen = new Pen(Color.FromArgb(200, 225, 29, 72), 3);
            g.DrawLine(slashPen, bounds.Left + 8, bounds.Bottom - 8, bounds.Right - 8, bounds.Top + 8);
        }
        else
        {
            // Empty dashed circle placeholder
            using var dashPen = new Pen(_isEnemy ? Color.FromArgb(160, 225, 29, 72) : Color.FromArgb(160, 6, 182, 212), 1.5f)
            {
                DashStyle = DashStyle.Dash
            };
            g.DrawEllipse(dashPen, bounds);

            // Slot number
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString($"B{_slotIndex + 1}", font, brush, bounds, sf);
        }

        // Outer Ring Border
        var ringColor = _isHovered 
            ? Color.White 
            : (_hero != null ? (_isEnemy ? EnemyBorder : AllyBorder) : BorderDefault);

        using var borderPen = new Pen(ringColor, _isHovered ? 2f : 1.5f);
        g.DrawEllipse(borderPen, bounds);
    }
}
