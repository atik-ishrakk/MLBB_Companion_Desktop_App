using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using MLBBCompanion.GUI.Dialogs;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Pages;

public class CheckerPage : UserControl
{
    private readonly IVisionEngine _visionEngine;
    private readonly IScreenCaptureProvider _screenCapture;
    private readonly IRoiConfigurationService _roiConfig;
    private readonly IPhaseAnchorStorageService _phaseStorage;

    // Loaded image and results
    private Bitmap? _currentFrameBmp;
    private CapturedFrame? _currentCapturedFrame;
    private readonly List<SlotMatch> _lastMatches = new();
    private Rectangle? _selectionRect;
    private Point _dragStart;
    private bool _isDragging = false;

    // Canvas Control
    private readonly PictureBox _canvas;
    private readonly Button _btnCanvasSettings;

    // Controls
    private readonly Button _btnLiveScan;
    private readonly Button _btnUpload;
    private readonly Button _btnSample;
    private readonly Button _btnTestPhase;
    private readonly Button _btnTopSettings;
    private readonly ContextMenuStrip _menuSettings;
    private readonly Label _lblPhaseResult;
    private readonly Label _lblRoiCoords;
    private readonly DataGridView _gridResults;

    // Right Cards
    private readonly Panel _cardThresholds;
    private readonly Panel _cardRoi;

    // Calibration Threshold Controls
    private readonly NumericUpDown _numBanThreshold;
    private readonly NumericUpDown _numPickThreshold;
    private readonly NumericUpDown _numEmptyBanThreshold;
    private readonly NumericUpDown _numEmptyPickAllyThreshold;
    private readonly NumericUpDown _numEmptyPickEnemyThreshold;
    private readonly NumericUpDown _numMinEdgeDensity;
    private readonly Button _btnSaveRois;
    private readonly Button _btnResetRois;
    private readonly Button _btnCropAnchor;

    // Theme Colors
    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);
    private static readonly Color BgCard = Color.FromArgb(20, 29, 47);
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color RedAccent = Color.FromArgb(225, 29, 72);
    private static readonly Color GoldAccent = Color.FromArgb(245, 158, 11);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public CheckerPage(
        IVisionEngine visionEngine,
        IScreenCaptureProvider screenCapture,
        IRoiConfigurationService roiConfig,
        IPhaseAnchorStorageService phaseStorage)
    {
        _visionEngine = visionEngine;
        _screenCapture = screenCapture;
        _roiConfig = roiConfig;
        _phaseStorage = phaseStorage;

        Dock = DockStyle.Fill;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;

        // 1. Top Bar
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(16, 8, 16, 8)
        };
        Controls.Add(topBar);

        _btnLiveScan = new Button
        {
            Text = "📸 Live BlueStacks Scan",
            Size = new Size(160, 28),
            Location = new Point(16, 10),
            BackColor = CyanAccent,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnLiveScan.FlatAppearance.BorderSize = 0;
        _btnLiveScan.Click += async (_, _) => await RunLiveScanAsync();
        topBar.Controls.Add(_btnLiveScan);

        _btnUpload = new Button
        {
            Text = "📁 Upload Screenshot",
            Size = new Size(140, 28),
            Location = new Point(184, 10),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnUpload.FlatAppearance.BorderSize = 1;
        _btnUpload.FlatAppearance.BorderColor = BorderColor;
        _btnUpload.Click += (_, _) => LoadScreenshotFromFile();
        topBar.Controls.Add(_btnUpload);

        _btnSample = new Button
        {
            Text = "🎯 Load Sample 1080p",
            Size = new Size(150, 28),
            Location = new Point(332, 10),
            BackColor = BgCard,
            ForeColor = TextMuted,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnSample.FlatAppearance.BorderSize = 1;
        _btnSample.FlatAppearance.BorderColor = BorderColor;
        _btnSample.Click += (_, _) => LoadSampleDraft(runCv: true);
        topBar.Controls.Add(_btnSample);

        _btnTopSettings = new Button
        {
            Text = "⚙ Settings ▾",
            Size = new Size(115, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(topBar.Width - 130, 10),
            BackColor = Color.FromArgb(20, 30, 48),
            ForeColor = CyanAccent,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnTopSettings.FlatAppearance.BorderSize = 1;
        _btnTopSettings.FlatAppearance.BorderColor = CyanAccent;
        topBar.Controls.Add(_btnTopSettings);

        _lblPhaseResult = new Label
        {
            Text = "Phase: Standby (1.00)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(topBar.Width - 310, 14),
            AutoSize = true
        };
        topBar.Controls.Add(_lblPhaseResult);

        // 2. Main Workspace Split (Left Canvas, Right Calibration & Results) - 50/50 split
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        Controls.Add(mainSplit);
        mainSplit.BringToFront();

        void AdjustSplitter()
        {
            if (mainSplit.Width > 100)
            {
                try
                {
                    mainSplit.SplitterDistance = Math.Max(50, mainSplit.Width / 2);
                }
                catch { }
            }
        }

        mainSplit.SizeChanged += (_, _) => AdjustSplitter();
        this.SizeChanged += (_, _) => AdjustSplitter();
        this.HandleCreated += (_, _) => BeginInvoke(new Action(AdjustSplitter));

        // Canvas PictureBox
        _canvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 12, 22),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Cross
        };
        _canvas.Paint += OnCanvasPaint;
        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;
        mainSplit.Panel1.Controls.Add(_canvas);

        // Top-right Setting button on Screenshot Canvas
        _btnCanvasSettings = new Button
        {
            Text = "⚙ Settings ▾",
            Size = new Size(115, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Math.Max(10, mainSplit.Panel1.Width - 128), 10),
            BackColor = Color.FromArgb(20, 30, 48),
            ForeColor = CyanAccent,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnCanvasSettings.FlatAppearance.BorderSize = 1;
        _btnCanvasSettings.FlatAppearance.BorderColor = CyanAccent;
        mainSplit.Panel1.Controls.Add(_btnCanvasSettings);
        _btnCanvasSettings.BringToFront();

        mainSplit.Panel1.Resize += (_, _) =>
        {
            _btnCanvasSettings.Location = new Point(Math.Max(10, mainSplit.Panel1.Width - 128), 10);
        };

        // Right Sidebar: Scrollable container for cards
        var pnlRight = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            AutoScroll = true,
            Padding = new Padding(12)
        };
        mainSplit.Panel2.Controls.Add(pnlRight);

        // =========================================================================
        // CARD 1: DETECTION RESULTS TILE (Title strictly ABOVE Content)
        // =========================================================================
        var cardDetection = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BgCard,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(0)
        };

        var pnlDetectionHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.FromArgb(24, 34, 53),
            Padding = new Padding(8, 0, 8, 0)
        };

        var lblDetectionTitle = new Label
        {
            Text = "📊 DETECTION RESULTS (21 ROWS)",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 7, 0, 0)
        };
        pnlDetectionHeader.Controls.Add(lblDetectionTitle);

        var lblDetectionSub = new Label
        {
            Text = "10 Bans | 10 Picks | 1 Game Phase",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextMuted,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 8, 0, 0)
        };
        pnlDetectionHeader.Controls.Add(lblDetectionSub);

        _gridResults = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 560,
            BackgroundColor = BgCard,
            ForeColor = TextPrimary,
            GridColor = BorderColor,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _gridResults.RowTemplate.Height = 25;
        _gridResults.DefaultCellStyle.BackColor = BgCard;
        _gridResults.DefaultCellStyle.ForeColor = TextPrimary;
        _gridResults.DefaultCellStyle.SelectionBackColor = Color.FromArgb(14, 116, 144);
        _gridResults.DefaultCellStyle.SelectionForeColor = Color.White;
        _gridResults.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _gridResults.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _gridResults.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        _gridResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _gridResults.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _gridResults.EnableHeadersVisualStyles = false;

        _gridResults.Columns.Add("Slot", "Slot");
        _gridResults.Columns.Add("Category", "Category");
        _gridResults.Columns.Add("Type", "Type");
        _gridResults.Columns.Add("Hero", "Detected Hero");
        _gridResults.Columns.Add("Conf", "Confidence");
        _gridResults.Columns.Add("P1", "First Priority");
        _gridResults.Columns.Add("P2", "Second Priority");
        _gridResults.Columns.Add("P3", "Third Priority");
        _gridResults.Columns.Add("Status", "Status");

        _gridResults.Columns["Slot"]!.FillWeight = 42;
        _gridResults.Columns["Category"]!.FillWeight = 42;
        _gridResults.Columns["Type"]!.FillWeight = 40;
        _gridResults.Columns["Hero"]!.FillWeight = 75;
        _gridResults.Columns["Conf"]!.FillWeight = 50;
        _gridResults.Columns["P1"]!.FillWeight = 90;
        _gridResults.Columns["P2"]!.FillWeight = 90;
        _gridResults.Columns["P3"]!.FillWeight = 90;
        _gridResults.Columns["Status"]!.FillWeight = 50;

        foreach (DataGridViewColumn col in _gridResults.Columns)
        {
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        // Add Grid first, then Header -> Header BringToFront guarantees Title is strictly ABOVE Content
        cardDetection.Controls.Add(_gridResults);
        cardDetection.Controls.Add(pnlDetectionHeader);
        pnlDetectionHeader.BringToFront();

        // =========================================================================
        // CARD 2: ROI CALIBRATION TILE (Title strictly ABOVE Content)
        // =========================================================================
        _cardRoi = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BgCard,
            Margin = new Padding(0, 10, 0, 10),
            Padding = new Padding(0)
        };

        var pnlRoiHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.FromArgb(24, 34, 53),
            Padding = new Padding(8, 0, 8, 0)
        };

        var lblRoiTitle = new Label
        {
            Text = "🎯 ROI CALIBRATION & COORDINATES",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 7, 0, 0)
        };
        pnlRoiHeader.Controls.Add(lblRoiTitle);

        var pnlRoiContent = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 6, 10, 6)
        };

        _lblRoiCoords = new Label
        {
            Text = "Drag on image canvas to draw crop box | Active Selection: [None]",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(215, 225, 240),
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlRoiContent.Controls.Add(_lblRoiCoords);

        var pnlRoiButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        pnlRoiContent.Controls.Add(pnlRoiButtons);

        _btnCropAnchor = new Button
        {
            Text = "✂️ Crop Selection as Anchor",
            Size = new Size(180, 28),
            BackColor = Color.FromArgb(14, 116, 144),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 8, 0)
        };
        _btnCropAnchor.FlatAppearance.BorderSize = 0;
        _btnCropAnchor.Click += async (_, _) => await CropSelectionAsAnchorAsync();
        pnlRoiButtons.Controls.Add(_btnCropAnchor);

        _btnTestPhase = new Button
        {
            Text = "⚡ Detect Phase",
            Size = new Size(110, 28),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = GoldAccent,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 0)
        };
        _btnTestPhase.FlatAppearance.BorderSize = 1;
        _btnTestPhase.FlatAppearance.BorderColor = BorderColor;
        _btnTestPhase.Click += (_, _) => TestPhaseDetection();
        pnlRoiButtons.Controls.Add(_btnTestPhase);

        _cardRoi.Controls.Add(pnlRoiContent);
        _cardRoi.Controls.Add(pnlRoiHeader);
        pnlRoiHeader.BringToFront();

        // =========================================================================
        // CARD 3: THRESHOLD CALIBRATION TILE (Title strictly ABOVE Content)
        // =========================================================================
        _cardThresholds = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BgCard,
            Margin = new Padding(0, 10, 0, 10),
            Padding = new Padding(0)
        };

        var pnlThresholdHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.FromArgb(24, 34, 53),
            Padding = new Padding(8, 0, 8, 0)
        };

        var lblThresholdTitle = new Label
        {
            Text = "⚙️ THRESHOLD CALIBRATION",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = GoldAccent,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 7, 0, 0)
        };
        pnlThresholdHeader.Controls.Add(lblThresholdTitle);

        var calibPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 6,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 8, 10, 8)
        };

        var activeRoi = _roiConfig.GetCurrentConfiguration();

        decimal banThresh = GetThresholdDecimal(activeRoi.Thresholds, "ban_threshold", 0.45m);
        decimal pickThresh = GetThresholdDecimal(activeRoi.Thresholds, "pick_threshold", 0.50m);
        decimal emptyBanThresh = GetThresholdDecimal(activeRoi.Thresholds, "empty_ban_threshold", 0.45m);
        decimal emptyAllyThresh = GetThresholdDecimal(activeRoi.Thresholds, "empty_pick_ally_threshold", 0.50m);
        decimal emptyEnemyThresh = GetThresholdDecimal(activeRoi.Thresholds, "empty_pick_enemy_threshold", 0.55m);
        decimal minEdge = GetThresholdDecimal(activeRoi.Thresholds, "min_edge_density", 0.025m);

        _numBanThreshold = CreateNumeric(calibPanel, "Ban Threshold", banThresh, 0.10m, 1.00m, 0.05m, 0);
        _numPickThreshold = CreateNumeric(calibPanel, "Pick Threshold", pickThresh, 0.10m, 1.00m, 0.05m, 1);
        _numEmptyBanThreshold = CreateNumeric(calibPanel, "Empty Ban Thresh", emptyBanThresh, 0.10m, 1.00m, 0.05m, 2);
        _numEmptyPickAllyThreshold = CreateNumeric(calibPanel, "Empty Ally Pick", emptyAllyThresh, 0.10m, 1.00m, 0.05m, 3);
        _numEmptyPickEnemyThreshold = CreateNumeric(calibPanel, "Empty Enemy Pick", emptyEnemyThresh, 0.10m, 1.00m, 0.05m, 4);
        _numMinEdgeDensity = CreateNumeric(calibPanel, "Min Edge Density", minEdge, 0.005m, 0.200m, 0.005m, 5);

        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 4, 10, 6)
        };

        _btnSaveRois = new Button
        {
            Text = "💾 Save Thresholds",
            Size = new Size(130, 28),
            BackColor = CyanAccent,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnSaveRois.FlatAppearance.BorderSize = 0;
        _btnSaveRois.Click += (_, _) => SaveRois();
        pnlButtons.Controls.Add(_btnSaveRois);

        _btnResetRois = new Button
        {
            Text = "🔄 Reset Defaults",
            Size = new Size(120, 28),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnResetRois.FlatAppearance.BorderSize = 1;
        _btnResetRois.FlatAppearance.BorderColor = BorderColor;
        _btnResetRois.Click += (_, _) => ResetRois();
        pnlButtons.Controls.Add(_btnResetRois);

        _cardThresholds.Controls.Add(pnlButtons);
        _cardThresholds.Controls.Add(calibPanel);
        _cardThresholds.Controls.Add(pnlThresholdHeader);
        pnlThresholdHeader.BringToFront();

        // Add Cards into Right Panel with explicit ChildIndex so titles are strictly above content
        pnlRight.Controls.Add(_cardThresholds);
        pnlRight.Controls.Add(_cardRoi);
        pnlRight.Controls.Add(cardDetection);

        pnlRight.Controls.SetChildIndex(cardDetection, 0);
        pnlRight.Controls.SetChildIndex(_cardRoi, 1);
        pnlRight.Controls.SetChildIndex(_cardThresholds, 2);

        // =========================================================================
        // SETTINGS MENU (Context dropdown triggered by Settings buttons)
        // =========================================================================
        _menuSettings = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(17, 24, 39),
            ForeColor = TextPrimary,
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9f)
        };
        _menuSettings.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());

        var mnuItemThresh = new ToolStripMenuItem("⚙️ Threshold Calibration (Toggle Panel)");
        mnuItemThresh.Click += (_, _) =>
        {
            _cardThresholds.Visible = !_cardThresholds.Visible;
            if (_cardThresholds.Visible) pnlRight.ScrollControlIntoView(_cardThresholds);
        };
        _menuSettings.Items.Add(mnuItemThresh);

        var mnuItemRoi = new ToolStripMenuItem("🎯 ROI Calibration & Coordinates (Toggle Panel)");
        mnuItemRoi.Click += (_, _) =>
        {
            _cardRoi.Visible = !_cardRoi.Visible;
            if (_cardRoi.Visible) pnlRight.ScrollControlIntoView(_cardRoi);
        };
        _menuSettings.Items.Add(mnuItemRoi);

        _menuSettings.Items.Add(new ToolStripSeparator());

        var mnuItemCrop = new ToolStripMenuItem("✂️ Crop Selection as Anchor");
        mnuItemCrop.Click += async (_, _) => await CropSelectionAsAnchorAsync();
        _menuSettings.Items.Add(mnuItemCrop);

        var mnuItemDetect = new ToolStripMenuItem("⚡ Detect Phase");
        mnuItemDetect.Click += (_, _) => TestPhaseDetection();
        _menuSettings.Items.Add(mnuItemDetect);

        var mnuItemSample = new ToolStripMenuItem("🎯 Load Sample 1080p Screenshot");
        mnuItemSample.Click += (_, _) => LoadSampleDraft(runCv: true);
        _menuSettings.Items.Add(mnuItemSample);

        _menuSettings.Items.Add(new ToolStripSeparator());

        var mnuItemSave = new ToolStripMenuItem("💾 Save Thresholds to Disk");
        mnuItemSave.Click += (_, _) => SaveRois();
        _menuSettings.Items.Add(mnuItemSave);

        var mnuItemReset = new ToolStripMenuItem("🔄 Reset Default Thresholds");
        mnuItemReset.Click += (_, _) => ResetRois();
        _menuSettings.Items.Add(mnuItemReset);

        _btnCanvasSettings.Click += (_, _) => _menuSettings.Show(_btnCanvasSettings, new Point(0, _btnCanvasSettings.Height));
        _btnTopSettings.Click += (_, _) => _menuSettings.Show(_btnTopSettings, new Point(0, _btnTopSettings.Height));
    }

    private NumericUpDown CreateNumeric(TableLayoutPanel table, string labelText, decimal value, decimal min, decimal max, decimal step, int row)
    {
        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
            ForeColor = Color.FromArgb(215, 225, 240),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.Controls.Add(lbl, 0, row);

        var num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = 3,
            Increment = step,
            Value = Math.Clamp(value, min, max),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 8.8f),
            Dock = DockStyle.Fill
        };
        num.ValueChanged += (_, _) => _canvas.Invalidate();
        table.Controls.Add(num, 1, row);
        return num;
    }

    private NumericUpDown CreateNumeric(TableLayoutPanel table, string labelText, int value, int min, int max, int row)
    {
        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 8.2f),
            ForeColor = TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.Controls.Add(lbl, 0, row);

        var num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 8.5f),
            Dock = DockStyle.Fill
        };
        num.ValueChanged += (_, _) => _canvas.Invalidate();
        table.Controls.Add(num, 1, row);
        return num;
    }

    private void LoadSampleDraft(bool runCv = false)
    {
        var path = WorkspacePathResolver.ResolvePath("Assets", "real_user_draft_1080.png");
        if (File.Exists(path))
        {
            LoadImageFromPath(path, runCv);
        }
    }

    private void LoadScreenshotFromFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select MLBB Screenshot",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            LoadImageFromPath(ofd.FileName, runCv: true);
        }
    }

    private void LoadImageFromPath(string path, bool runCv = true)
    {
        try
        {
            using var fileBmp = new Bitmap(path);
            _currentFrameBmp?.Dispose();
            _currentFrameBmp = new Bitmap(fileBmp);

            using var ms = new MemoryStream();
            fileBmp.Save(ms, ImageFormat.Png);
            _currentCapturedFrame = new CapturedFrame
            {
                Buffer = ms.ToArray(),
                Width = fileBmp.Width,
                Height = fileBmp.Height,
                Source = "file"
            };

            _canvas.Image = _currentFrameBmp;
            if (runCv)
            {
                RunCvOnCurrentFrame();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load image:\n{ex.Message}", "Checker Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunLiveScanAsync()
    {
        _btnLiveScan.Enabled = false;
        try
        {
            var frame = await _screenCapture.CaptureActiveWindowAsync();
            if (frame != null && frame.IsValid && frame.Buffer != null)
            {
                using var ms = new MemoryStream(frame.Buffer);
                _currentFrameBmp?.Dispose();
                _currentFrameBmp = new Bitmap(ms);
                _currentCapturedFrame = frame;
                _canvas.Image = _currentFrameBmp;

                _lblPhaseResult.Text = $"Captured via {frame.Source} ({frame.Width}x{frame.Height} HD)";
                RunCvOnCurrentFrame();
            }
            else
            {
                var diag = _screenCapture.LastDiagnostics;
                MessageBox.Show($"Could not capture emulator screen.\nDetails: {diag.Message}\nADB Status: {(diag.AdbConnected ? "Connected" : "Disconnected")}\nWindow Found: {(diag.BlueStacksWindowFound ? "Yes" : "No")}\nMethod: {diag.MethodAttempted}", "Live Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Live scan failed:\n{ex.Message}", "Live Scan", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnLiveScan.Enabled = true;
        }
    }

    private void RunCvOnCurrentFrame()
    {
        if (_currentCapturedFrame == null) return;

        var config = _roiConfig.GetCurrentConfiguration();
        _lastMatches.Clear();
        _gridResults.Rows.Clear();

        try
        {
            double banThresh = (double)_numBanThreshold.Value;
            double pickThresh = (double)_numPickThreshold.Value;

            // Match Bans (10 slots)
            var banMatches = _visionEngine.MatchBans(_currentCapturedFrame, config, threshold: banThresh);
            _lastMatches.AddRange(banMatches);

            // Match Picks (10 slots)
            var (allyPicks, enemyPicks, _) = _visionEngine.MatchPicksJoint(_currentCapturedFrame, config, new HashSet<string>(), threshold: pickThresh);
            _lastMatches.AddRange(allyPicks);
            _lastMatches.AddRange(enemyPicks);

            // 1. Ally Bans (Slots 0..4) - 5 Rows
            for (int i = 0; i < 5; i++)
            {
                var m = banMatches.FirstOrDefault(b => b.Slot == i) ?? new SlotMatch { Slot = i, Side = "ally", Shape = "round" };
                AddGridRow($"Ban {i + 1}", "Ally", "Ban", m);
            }

            // 2. Enemy Bans (Slots 5..9) - 5 Rows
            for (int i = 5; i < 10; i++)
            {
                var m = banMatches.FirstOrDefault(b => b.Slot == i) ?? new SlotMatch { Slot = i, Side = "enemy", Shape = "round" };
                AddGridRow($"Ban {i + 1}", "Enemy", "Ban", m);
            }

            // 3. Ally Picks (Slots 0..4) - 5 Rows
            for (int i = 0; i < 5; i++)
            {
                var m = (i < allyPicks.Count) ? allyPicks[i] : new SlotMatch { Slot = i, Side = "ally", Shape = "rectangle" };
                AddGridRow($"Pick {i + 1}", "Ally", "Pick", m);
            }

            // 4. Enemy Picks (Slots 5..9) - 5 Rows
            for (int i = 0; i < 5; i++)
            {
                var m = (i < enemyPicks.Count) ? enemyPicks[i] : new SlotMatch { Slot = i + 5, Side = "enemy", Shape = "rectangle" };
                AddGridRow($"Pick {i + 6}", "Enemy", "Pick", m);
            }

            // 5. Game Phase (Row 21) - 1 Row (Total = 21 Rows)
            var (phase, subPhase, phaseConf, phaseDetails) = _visionEngine.DetectPhase(_currentCapturedFrame);
            _lblPhaseResult.Text = $"Phase: {phase} ({phaseConf:F2})";
            _lblPhaseResult.ForeColor = phaseConf > 0.8 ? CyanAccent : GoldAccent;

            string phaseStatus = phaseConf >= 0.65 ? "CONFIRMED" : (phaseConf > 0.3 ? "DETECTED" : "STANDBY");
            string p1Str = $"Anchor: {phase} ({phaseConf:P1})";
            string p2Str = subPhase != null ? $"Sub: {subPhase}" : "Sub: Main Phase";
            string p3Str = !string.IsNullOrEmpty(phaseDetails) ? phaseDetails : "Active Matcher Signal";

            _gridResults.Rows.Add(
                "Phase",
                "System",
                "Phase",
                $"{phase}" + (subPhase != null ? $" ({subPhase})" : ""),
                $"{phaseConf:P1}",
                p1Str,
                p2Str,
                p3Str,
                phaseStatus
            );

            _canvas.Invalidate();
        }
        catch (Exception ex)
        {
            _lblPhaseResult.Text = $"CV Error: {ex.Message}";
        }
    }

    private void AddGridRow(string slotLabel, string category, string type, SlotMatch m)
    {
        string heroName = !string.IsNullOrEmpty(m.Hero) && !m.Hero.Equals("Empty", StringComparison.OrdinalIgnoreCase)
            ? m.Hero
            : "Empty";

        string status;
        if (heroName != "Empty")
        {
            status = m.Confidence >= 0.60 ? "LOCKED" : "DETECTED";
        }
        else
        {
            status = !string.IsNullOrEmpty(m.RejectionReason) ? m.RejectionReason : "EMPTY";
        }

        string p1 = FormatCandidate(m.TopKCandidates, 0);
        string p2 = FormatCandidate(m.TopKCandidates, 1);
        string p3 = FormatCandidate(m.TopKCandidates, 2);

        _gridResults.Rows.Add(
            slotLabel,
            category,
            type,
            heroName,
            $"{m.Confidence:P1}",
            p1,
            p2,
            p3,
            status
        );
    }

    private static string FormatCandidate(List<CandidateMatch>? cands, int index)
    {
        if (cands != null && index >= 0 && index < cands.Count)
        {
            var c = cands[index];
            if (!string.IsNullOrWhiteSpace(c.Hero))
            {
                return $"{c.Hero} ({c.Score:P1})";
            }
        }
        return "-";
    }

    private void TestPhaseDetection()
    {
        if (_currentCapturedFrame == null) return;
        try
        {
            var (phase, subPhase, conf, _) = _visionEngine.DetectPhase(_currentCapturedFrame);
            _lblPhaseResult.Text = $"Phase: {phase} ({conf:F2})";
            _lblPhaseResult.ForeColor = conf > 0.8 ? CyanAccent : GoldAccent;
        }
        catch
        {
            _lblPhaseResult.Text = "Phase: Unknown";
        }
    }

    private void SaveRois()
    {
        var cfg = _roiConfig.GetCurrentConfiguration();
        cfg.Thresholds["ban_threshold"] = (double)_numBanThreshold.Value;
        cfg.Thresholds["pick_threshold"] = (double)_numPickThreshold.Value;
        cfg.Thresholds["empty_ban_threshold"] = (double)_numEmptyBanThreshold.Value;
        cfg.Thresholds["empty_pick_ally_threshold"] = (double)_numEmptyPickAllyThreshold.Value;
        cfg.Thresholds["empty_pick_enemy_threshold"] = (double)_numEmptyPickEnemyThreshold.Value;
        cfg.Thresholds["min_edge_density"] = (double)_numMinEdgeDensity.Value;

        _roiConfig.SaveConfiguration(cfg);
        MessageBox.Show("Thresholds saved successfully.", "Calibration Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ResetRois()
    {
        var cfg = _roiConfig.ResetToDefault();
        _numBanThreshold.Value = GetThresholdDecimal(cfg.Thresholds, "ban_threshold", 0.45m);
        _numPickThreshold.Value = GetThresholdDecimal(cfg.Thresholds, "pick_threshold", 0.50m);
        _numEmptyBanThreshold.Value = GetThresholdDecimal(cfg.Thresholds, "empty_ban_threshold", 0.45m);
        _numEmptyPickAllyThreshold.Value = GetThresholdDecimal(cfg.Thresholds, "empty_pick_ally_threshold", 0.50m);
        _numEmptyPickEnemyThreshold.Value = GetThresholdDecimal(cfg.Thresholds, "empty_pick_enemy_threshold", 0.55m);
        _numMinEdgeDensity.Value = GetThresholdDecimal(cfg.Thresholds, "min_edge_density", 0.025m);

        _canvas.Invalidate();
        MessageBox.Show("Thresholds restored to defaults.", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CropSelectionAsAnchorAsync()
    {
        if (_selectionRect == null || _currentFrameBmp == null)
        {
            MessageBox.Show("Please click and drag on the image canvas to draw a crop box first.", "Crop Anchor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var rect = _selectionRect.Value;
        if (rect.Width < 20 || rect.Height < 20) return;

        using var dlg = new CropAnchorDialog(rect);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                using var cropped = new Bitmap(rect.Width, rect.Height);
                using (var g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(_currentFrameBmp, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
                }

                using var ms = new MemoryStream();
                cropped.Save(ms, ImageFormat.Png);
                string b64 = Convert.ToBase64String(ms.ToArray());

                var req = new CropAnchorRequest
                {
                    Name = dlg.AnchorName,
                    Phase = dlg.TargetPhase,
                    Threshold = dlg.MinConfidence,
                    X1 = rect.X,
                    Y1 = rect.Y,
                    X2 = rect.Right,
                    Y2 = rect.Bottom,
                    ImageBase64 = b64
                };

                var (success, msg, _) = _phaseStorage.CropAndSaveAnchorFromFrame(req);
                if (success)
                {
                    _visionEngine.ReloadTemplates();
                    MessageBox.Show($"Phase anchor '{dlg.AnchorName}' saved for phase '{dlg.TargetPhase}'.", "Anchor Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to save anchor: {msg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save anchor:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #region Canvas Painting & Mouse Interaction

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_currentFrameBmp == null)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var bgBrush = new SolidBrush(Color.FromArgb(8, 12, 22));
            g.FillRectangle(bgBrush, _canvas.ClientRectangle);

            int w = _canvas.Width;
            int h = _canvas.Height;
            if (w > 120 && h > 100)
            {
                float targetAspect = 16f / 9f;
                float currentAspect = (float)w / h;
                int frameW, frameH;
                if (currentAspect > targetAspect)
                {
                    frameH = (int)(h * 0.72f);
                    frameW = (int)(frameH * targetAspect);
                }
                else
                {
                    frameW = (int)(w * 0.85f);
                    frameH = (int)(frameW / targetAspect);
                }

                int frameX = (w - frameW) / 2;
                int frameY = (h - frameH) / 2;
                var frameRect = new Rectangle(frameX, frameY, frameW, frameH);

                using var innerBrush = new SolidBrush(Color.FromArgb(13, 19, 33));
                g.FillRectangle(innerBrush, frameRect);

                using var dashPen = new Pen(Color.FromArgb(51, 65, 85), 2) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(dashPen, frameRect);

                using var iconFont = new Font("Segoe UI", 26f);
                using var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
                using var subFont = new Font("Segoe UI", 8.8f, FontStyle.Regular);
                using var textBrush = new SolidBrush(Color.FromArgb(203, 213, 225));
                using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                using var cyanBrush = new SolidBrush(CyanAccent);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                g.DrawString("📷", iconFont, cyanBrush, new RectangleF(frameX, frameY + frameH / 2 - 55, frameW, 40), sf);
                g.DrawString("NO SCREENSHOT LOADED", titleFont, textBrush, new RectangleF(frameX, frameY + frameH / 2 - 12, frameW, 25), sf);
                g.DrawString("Click '📸 Live BlueStacks Scan' or '📁 Upload Screenshot' to analyze draft", subFont, subBrush, new RectangleF(frameX, frameY + frameH / 2 + 16, frameW, 25), sf);
                g.DrawString("Resolution: 1920 × 1080 (16:9 FHD)", subFont, cyanBrush, new RectangleF(frameX, frameY + frameH / 2 + 40, frameW, 25), sf);
            }
            return;
        }

        // Calculate aspect ratio scale and offsets of PictureBoxSizeMode.Zoom
        float scale = Math.Min((float)_canvas.Width / _currentFrameBmp.Width, (float)_canvas.Height / _currentFrameBmp.Height);
        float offsetX = (_canvas.Width - (_currentFrameBmp.Width * scale)) / 2f;
        float offsetY = (_canvas.Height - (_currentFrameBmp.Height * scale)) / 2f;

        // 1. Draw Active Bans ROIs
        using var allyPen = new Pen(CyanAccent, 2);
        using var enemyPen = new Pen(RedAccent, 2);
        using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);

        for (int i = 0; i < 5; i++)
        {
            var pA = _roiConfig.GetRoiPixels(1920, 1080, $"ally_ban_{i}");
            if (pA.Width > 0)
            {
                var rectA = ScaleRect(new Rectangle(pA.X, pA.Y, pA.Width, pA.Height), scale, offsetX, offsetY);
                g.DrawEllipse(allyPen, rectA);
                g.DrawString($"A-B{i + 1}", font, brush, rectA.Left, rectA.Top - 14);
            }

            var pE = _roiConfig.GetRoiPixels(1920, 1080, $"enemy_ban_{i}");
            if (pE.Width > 0)
            {
                var rectE = ScaleRect(new Rectangle(pE.X, pE.Y, pE.Width, pE.Height), scale, offsetX, offsetY);
                g.DrawEllipse(enemyPen, rectE);
                g.DrawString($"E-B{i + 1}", font, brush, rectE.Left, rectE.Top - 14);
            }
        }

        // 2. Draw Active Picks ROIs
        for (int i = 0; i < 5; i++)
        {
            var pPickA = _roiConfig.GetRoiPixels(1920, 1080, $"ally_pick_{i}");
            if (pPickA.Width > 0)
            {
                var rectPickA = ScaleRect(new Rectangle(pPickA.X, pPickA.Y, pPickA.Width, pPickA.Height), scale, offsetX, offsetY);
                g.DrawRectangle(allyPen, rectPickA);
                g.DrawString($"Ally {i + 1}", font, brush, rectPickA.Left, rectPickA.Top - 14);
            }

            var pPickE = _roiConfig.GetRoiPixels(1920, 1080, $"enemy_pick_{i}");
            if (pPickE.Width > 0)
            {
                var rectPickE = ScaleRect(new Rectangle(pPickE.X, pPickE.Y, pPickE.Width, pPickE.Height), scale, offsetX, offsetY);
                g.DrawRectangle(enemyPen, rectPickE);
                g.DrawString($"Enemy {i + 1}", font, brush, rectPickE.Left, rectPickE.Top - 14);
            }
        }

        // 3. Draw Crop Selection Box
        if (_selectionRect.HasValue)
        {
            var scaledCrop = ScaleRect(_selectionRect.Value, scale, offsetX, offsetY);
            using var cropPen = new Pen(GoldAccent, 2) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(cropPen, scaledCrop);
            g.DrawString($"Crop: {_selectionRect.Value.Width}×{_selectionRect.Value.Height}px", font, new SolidBrush(GoldAccent), scaledCrop.Left, scaledCrop.Bottom + 4);
        }
    }

    private static Rectangle ScaleRect(Rectangle r, float scale, float ox, float oy)
    {
        return new Rectangle(
            (int)(r.X * scale + ox),
            (int)(r.Y * scale + oy),
            (int)(r.Width * scale),
            (int)(r.Height * scale)
        );
    }

    private Point ImageCoordsFromCanvas(Point canvasPt)
    {
        if (_currentFrameBmp == null) return canvasPt;
        float scale = Math.Min((float)_canvas.Width / _currentFrameBmp.Width, (float)_canvas.Height / _currentFrameBmp.Height);
        float offsetX = (_canvas.Width - (_currentFrameBmp.Width * scale)) / 2f;
        float offsetY = (_canvas.Height - (_currentFrameBmp.Height * scale)) / 2f;

        int imgX = (int)((canvasPt.X - offsetX) / scale);
        int imgY = (int)((canvasPt.Y - offsetY) / scale);

        return new Point(
            Math.Clamp(imgX, 0, _currentFrameBmp.Width),
            Math.Clamp(imgY, 0, _currentFrameBmp.Height)
        );
    }

    private void OnCanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _currentFrameBmp != null)
        {
            _dragStart = ImageCoordsFromCanvas(e.Location);
            _isDragging = true;
            _selectionRect = null;
            _lblRoiCoords.Text = "Active Selection: Dragging on canvas...";
        }
    }

    private void OnCanvasMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging && _currentFrameBmp != null)
        {
            var current = ImageCoordsFromCanvas(e.Location);
            int x = Math.Min(_dragStart.X, current.X);
            int y = Math.Min(_dragStart.Y, current.Y);
            int w = Math.Abs(_dragStart.X - current.X);
            int h = Math.Abs(_dragStart.Y - current.Y);

            _selectionRect = new Rectangle(x, y, w, h);
            _lblRoiCoords.Text = $"Active Selection: [X: {x}, Y: {y}, W: {w}, H: {h}] (Drawing...)";
            _canvas.Invalidate();
        }
    }

    private void OnCanvasMouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            if (_selectionRect.HasValue && _selectionRect.Value.Width > 5 && _selectionRect.Value.Height > 5)
            {
                var r = _selectionRect.Value;
                _lblRoiCoords.Text = $"Active Selection: [X: {r.X}, Y: {r.Y}, W: {r.Width}, H: {r.Height}] (Ready to Crop)";
            }
            else
            {
                _lblRoiCoords.Text = "Drag on image canvas to draw crop box | Active Selection: [None]";
            }
            _canvas.Invalidate();
        }
    }

    private static decimal GetThresholdDecimal(Dictionary<string, object> dict, string key, decimal fallback)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return fallback;
        if (val is JsonElement elem)
        {
            if (elem.TryGetDecimal(out var d)) return d;
            if (elem.TryGetDouble(out var db)) return (decimal)db;
            if (decimal.TryParse(elem.ToString(), out var parsed)) return parsed;
        }
        try { return Convert.ToDecimal(val); } catch { return fallback; }
    }

    private class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(14, 116, 144);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(14, 116, 144);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(14, 116, 144);
        public override Color MenuItemBorder => Color.FromArgb(6, 182, 212);
        public override Color MenuBorder => Color.FromArgb(51, 65, 85);
        public override Color ToolStripDropDownBackground => Color.FromArgb(17, 24, 39);
        public override Color ImageMarginGradientBegin => Color.FromArgb(17, 24, 39);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(17, 24, 39);
        public override Color ImageMarginGradientEnd => Color.FromArgb(17, 24, 39);
        public override Color SeparatorDark => Color.FromArgb(51, 65, 85);
    }

    #endregion
}
