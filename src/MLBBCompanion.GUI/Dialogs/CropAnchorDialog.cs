namespace MLBBCompanion.GUI.Dialogs;

public class CropAnchorDialog : Form
{
    public string AnchorName { get; private set; } = string.Empty;
    public string TargetPhase { get; private set; } = "Draft Pick";
    public double MinConfidence { get; private set; } = 0.85;

    private readonly TextBox _txtName;
    private readonly ComboBox _cmbPhase;
    private readonly NumericUpDown _numConfidence;

    private static readonly Color BgDark = Color.FromArgb(15, 23, 42);
    private static readonly Color BgCard = Color.FromArgb(30, 41, 59);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);

    public CropAnchorDialog(Rectangle cropRect)
    {
        Text = "CROP NEW PHASE ANCHOR";
        Size = new Size(420, 280);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var lblTitle = new Label
        {
            Text = $"CROP ANCHOR ({cropRect.Width}×{cropRect.Height}px at [{cropRect.X},{cropRect.Y}])",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(16, 10, 0, 0)
        };
        Controls.Add(lblTitle);

        var lblName = new Label { Text = "Anchor Identifier Name:", Location = new Point(20, 50), AutoSize = true };
        Controls.Add(lblName);

        _txtName = new TextBox
        {
            Text = $"anchor_{DateTime.Now:HHmmss}",
            Location = new Point(20, 72),
            Size = new Size(360, 26),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_txtName);

        var lblPhase = new Label { Text = "Target Game Phase:", Location = new Point(20, 110), AutoSize = true };
        Controls.Add(lblPhase);

        _cmbPhase = new ComboBox
        {
            Location = new Point(20, 132),
            Size = new Size(220, 26),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbPhase.Items.AddRange(["Draft Pick", "Ban Phase", "Pick Phase", "In-Game Laning", "Standby"]);
        _cmbPhase.SelectedIndex = 0;
        Controls.Add(_cmbPhase);

        var lblConf = new Label { Text = "Min Conf:", Location = new Point(260, 110), AutoSize = true };
        Controls.Add(lblConf);

        _numConfidence = new NumericUpDown
        {
            Location = new Point(260, 132),
            Size = new Size(120, 26),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            DecimalPlaces = 2,
            Increment = 0.05m,
            Minimum = 0.50m,
            Maximum = 1.00m,
            Value = 0.85m
        };
        Controls.Add(_numConfidence);

        var btnSave = new Button
        {
            Text = "✔ Save Anchor",
            Location = new Point(260, 184),
            Size = new Size(120, 32),
            BackColor = CyanAccent,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            AnchorName = _txtName.Text.Trim();
            TargetPhase = _cmbPhase.SelectedItem?.ToString() ?? "Draft Pick";
            MinConfidence = (double)_numConfidence.Value;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(160, 184),
            Size = new Size(90, 32),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(btnCancel);
    }
}
