using System.Drawing.Drawing2D;

namespace MLBBCompanion.GUI.Controls;

public class WinProbabilityBar : Control
{
    private double _allyWinProb = 50.0;

    public double AllyWinProbability
    {
        get => _allyWinProb;
        set
        {
            _allyWinProb = Math.Clamp(value, 5.0, 95.0);
            Invalidate();
        }
    }

    private static readonly Color BgTrack = Color.FromArgb(15, 23, 42);
    private static readonly Color AllyFill = Color.FromArgb(6, 182, 212);    // Cyan
    private static readonly Color EnemyFill = Color.FromArgb(225, 29, 72);   // Crimson

    public WinProbabilityBar()
    {
        Size = new Size(300, 22);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width, Height);

        // Background track
        using (var bgBrush = new SolidBrush(BgTrack))
        {
            g.FillRectangle(bgBrush, bounds);
        }

        int allyWidth = (int)(Width * (_allyWinProb / 100.0));
        allyWidth = Math.Clamp(allyWidth, 6, Width - 6);

        // Ally Fill (Cyan)
        var allyRect = new Rectangle(0, 0, allyWidth, Height);
        using (var allyBrush = new SolidBrush(AllyFill))
        {
            g.FillRectangle(allyBrush, allyRect);
        }

        // Enemy Fill (Crimson)
        var enemyRect = new Rectangle(allyWidth, 0, Width - allyWidth, Height);
        using (var enemyBrush = new SolidBrush(EnemyFill))
        {
            g.FillRectangle(enemyBrush, enemyRect);
        }

        // Center split line
        using (var linePen = new Pen(Color.FromArgb(248, 250, 252), 2))
        {
            g.DrawLine(linePen, allyWidth, 0, allyWidth, Height);
        }

        // Border
        using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }
}
