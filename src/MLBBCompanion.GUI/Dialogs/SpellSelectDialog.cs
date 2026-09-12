using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Dialogs;

public class SpellSelectDialog : Form
{
    public string? SelectedSpellId { get; private set; }

    private static readonly (string Id, string Name, string Cooldown)[] Spells =
    [
        ("flicker", "Flicker", "120s"),
        ("retribution", "Retribution", "35s"),
        ("inspire", "Inspire", "75s"),
        ("sprint", "Sprint", "100s"),
        ("purify", "Purify", "90s"),
        ("aegis", "Aegis", "75s"),
        ("revitalize", "Revitalize", "100s"),
        ("petrify", "Petrify", "90s"),
        ("flameshot", "Flameshot", "50s"),
        ("vengeance", "Vengeance", "75s"),
        ("arrival", "Arrival", "75s"),
        ("execute", "Execute", "90s")
    ];

    private static readonly Color BgDark = Color.FromArgb(15, 23, 42);
    private static readonly Color BgCard = Color.FromArgb(30, 41, 59);
    private static readonly Color BgCardHover = Color.FromArgb(51, 65, 85);
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public SpellSelectDialog()
    {
        Text = "SELECT BATTLE SPELL";
        Size = new Size(540, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
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

        var lblTitle = new Label
        {
            Text = "SELECT BATTLE SPELL",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(16, 12, 0, 0)
        };
        Controls.Add(lblTitle);

        var grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(10, 15, 29)
        };
        Controls.Add(grid);
        grid.BringToFront();

        foreach (var (id, name, cd) in Spells)
        {
            var card = new Panel
            {
                Size = new Size(110, 106),
                Margin = new Padding(6),
                BackColor = BgCard,
                Cursor = Cursors.Hand
            };

            var pic = new PictureBox
            {
                Size = new Size(50, 50),
                Location = new Point(30, 8),
                Image = ImageCache.GetSpellImage(id, 50, 50),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            pic.Paint += (_, pe) =>
            {
                pe.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(71, 85, 105), 1.5f);
                pe.Graphics.DrawEllipse(pen, 1, 1, pic.Width - 3, pic.Height - 3);
            };

            var lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.TopCenter,
                Size = new Size(106, 18),
                Location = new Point(2, 62),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var lblCd = new Label
            {
                Text = cd,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.TopCenter,
                Size = new Size(106, 16),
                Location = new Point(2, 72),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            void OnSelect(object? sender, EventArgs e)
            {
                SelectedSpellId = id;
                DialogResult = DialogResult.OK;
                Close();
            }

            card.Click += OnSelect;
            pic.Click += OnSelect;
            lblName.Click += OnSelect;
            lblCd.Click += OnSelect;

            card.MouseEnter += (_, _) => { card.BackColor = BgCardHover; };
            card.MouseLeave += (_, _) => { card.BackColor = BgCard; };
            pic.MouseEnter += (_, _) => { card.BackColor = BgCardHover; };
            lblName.MouseEnter += (_, _) => { card.BackColor = BgCardHover; };

            card.Paint += (_, e) =>
            {
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            card.Controls.Add(pic);
            card.Controls.Add(lblName);
            card.Controls.Add(lblCd);
            grid.Controls.Add(card);
        }
    }
}
