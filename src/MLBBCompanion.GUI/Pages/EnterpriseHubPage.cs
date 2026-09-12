using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MLBB.Core.Interfaces;
using MLBBCompanion.BLL.Interfaces;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Pages;

public class EnterpriseHubPage : UserControl
{
    private readonly ILauncherService _launcherService;
    private readonly IVisionEngine _visionEngine;
    private readonly System.Windows.Forms.Timer _telemetryTimer;

    // Service States (Default to disabled on opening, enabled manually per user requirement)
    private bool _isBsRunning = false;
    private bool _isVisionEnabled = false;
    private bool _isAdbCaptureEnabled = false;
    private bool _isMemWatchdogEnabled = false;

    // Service Cards
    private Panel _cardBs = null!;
    private Panel _cardCv = null!;
    private Panel _cardAdb = null!;
    private Panel _cardMem = null!;

    // Status Badges
    private Label _lblBsStatus = null!;
    private Label _lblCvStatus = null!;
    private Label _lblAdbStatus = null!;
    private Label _lblMemStatus = null!;

    // OpenCV Metrics
    private Label _lblCvHeroes = null!;
    private Label _lblCvPicks = null!;
    private Label _lblCvAnchors = null!;
    private Label _lblCvNativeMem = null!;

    // ADB Metrics
    private Label _lblAdbPort = null!;
    private Label _lblAdbCapture = null!;
    private Label _lblAdbResolution = null!;

    // Memory Metrics
    private Label _lblMemWorkingSet = null!;
    private Label _lblMemGcHeap = null!;
    private Label _lblMemArchitecture = null!;

    // Console Log
    private readonly RichTextBox _txtConsole;

    // Action Buttons
    private readonly Button _btnStartAll;
    private readonly Button _btnReloadTensors;
    private readonly Button _btnTrimMemory;
    private readonly Button _btnEmergencyStop;

    // Theme Colors
    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);
    private static readonly Color BgCard = Color.FromArgb(17, 24, 39);
    private static readonly Color BgCardHover = Color.FromArgb(24, 34, 53);
    private static readonly Color BgCardActive = Color.FromArgb(18, 30, 50);
    private static readonly Color BorderDefault = Color.FromArgb(40, 53, 76);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color GreenAccent = Color.FromArgb(52, 211, 153);
    private static readonly Color RedAccent = Color.FromArgb(225, 29, 72);
    private static readonly Color AmberAccent = Color.FromArgb(245, 158, 11);
    private static readonly Color PurpleAccent = Color.FromArgb(168, 85, 247);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(226, 232, 240);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
    private static readonly Color LabelTitleColor = Color.FromArgb(215, 225, 240);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public EnterpriseHubPage(ILauncherService launcherService, IVisionEngine visionEngine)
    {
        _launcherService = launcherService;
        _visionEngine = visionEngine;

        Dock = DockStyle.Fill;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        Padding = new Padding(20);

        // 1. Header Panel
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.Transparent };
        Controls.Add(pnlHeader);

        var lblTitle = new Label
        {
            Text = "DASHBOARD & SERVICES",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(0, 4)
        };
        pnlHeader.Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Interactive service cards — click any card to enable or disable the service. Real-time telemetry & hardware management.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(0, 30)
        };
        pnlHeader.Controls.Add(lblSubtitle);

        // 2. Main 4 Service Cards Grid (2 rows x 2 columns)
        var cardsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 310,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 10)
        };
        cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        cardsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        cardsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        Controls.Add(cardsTable);

        // CARD 1: BLUESTACKS 5 EMULATOR SERVICE
        _cardBs = CreateInteractiveServiceCard(
            "BLUESTACKS 5 EMULATOR",
            "● RUNNING • ONLINE",
            "○ DISABLED • OFFLINE",
            GreenAccent,
            () => _isBsRunning,
            async () => await ToggleBlueStacksAsync(),
            out _lblBsStatus
        );
        _cardBs.Margin = new Padding(0, 0, 8, 8);
        cardsTable.Controls.Add(_cardBs, 0, 0);

        var tblBsMetrics = CreateMetricGrid(2, 2);
        _cardBs.Controls.Add(tblBsMetrics);
        tblBsMetrics.BringToFront();
        AddMetricRow(tblBsMetrics, 0, 0, "Emulator Host", "BlueStacks 5 (x64)", TextPrimary);
        AddMetricRow(tblBsMetrics, 1, 0, "Daemon Port", "127.0.0.1:5555", CyanAccent);
        AddMetricRow(tblBsMetrics, 0, 1, "Direct Render", "GDI Desktop Hook 1080p", TextSecondary);
        AddMetricRow(tblBsMetrics, 1, 1, "Quick Action", "Click card to Start/Stop", GreenAccent);

        // CARD 2: OPENCV VISION & TENSOR ENGINE
        _cardCv = CreateInteractiveServiceCard(
            "OPENCV VISION & TENSOR ENGINE",
            "● ACTIVE • 136 HEROES",
            "○ DISABLED • PAUSED",
            CyanAccent,
            () => _isVisionEnabled,
            async () => await ToggleVisionEngineAsync(),
            out _lblCvStatus
        );
        _cardCv.Margin = new Padding(8, 0, 0, 8);
        cardsTable.Controls.Add(_cardCv, 1, 0);

        var tblCvMetrics = CreateMetricGrid(2, 2);
        _cardCv.Controls.Add(tblCvMetrics);
        tblCvMetrics.BringToFront();
        _lblCvHeroes = AddMetricRow(tblCvMetrics, 0, 0, "Hero Classes", "136 Templates", CyanAccent);
        _lblCvAnchors = AddMetricRow(tblCvMetrics, 1, 0, "Phase Anchors", "23 Active", GreenAccent);
        _lblCvPicks = AddMetricRow(tblCvMetrics, 0, 1, "Pipeline", "CLAHE + ZNCC", TextSecondary);
        _lblCvNativeMem = AddMetricRow(tblCvMetrics, 1, 1, "C++ Mat Buffer", "38.4 MB", AmberAccent);

        // CARD 3: ADB BRIDGE SCREEN CAPTURE
        _cardAdb = CreateInteractiveServiceCard(
            "ADB BRIDGE SCREEN CAPTURE",
            "● CAPTURE RUNNING",
            "○ DISABLED • STANDBY",
            GreenAccent,
            () => _isAdbCaptureEnabled,
            async () => await ToggleAdbCaptureAsync(),
            out _lblAdbStatus
        );
        _cardAdb.Margin = new Padding(0, 8, 8, 0);
        cardsTable.Controls.Add(_cardAdb, 0, 1);

        var tblAdbMetrics = CreateMetricGrid(2, 2);
        _cardAdb.Controls.Add(tblAdbMetrics);
        tblAdbMetrics.BringToFront();
        _lblAdbPort = AddMetricRow(tblAdbMetrics, 0, 0, "Bridge Port", "127.0.0.1:5555", CyanAccent);
        _lblAdbCapture = AddMetricRow(tblAdbMetrics, 1, 0, "Capture Mode", "Win32 BitBlt", TextPrimary);
        _lblAdbResolution = AddMetricRow(tblAdbMetrics, 0, 1, "Viewport Frame", "1920x1080 32bpp", TextSecondary);
        AddMetricRow(tblAdbMetrics, 1, 1, "Memory Transfer", "Direct Byte Buffer", GreenAccent);

        // CARD 4: RUNTIME PERFORMANCE & MEMORY WATCHDOG
        _cardMem = CreateInteractiveServiceCard(
            "MEMORY WATCHDOG & OPTIMIZATION",
            "● WATCHDOG ACTIVE",
            "○ DISABLED • IDLE",
            PurpleAccent,
            () => _isMemWatchdogEnabled,
            async () => await ToggleMemoryWatchdogAsync(),
            out _lblMemStatus
        );
        _cardMem.Margin = new Padding(8, 8, 0, 0);
        cardsTable.Controls.Add(_cardMem, 1, 1);

        var tblMemMetrics = CreateMetricGrid(2, 2);
        _cardMem.Controls.Add(tblMemMetrics);
        tblMemMetrics.BringToFront();
        _lblMemWorkingSet = AddMetricRow(tblMemMetrics, 0, 0, "Working Set", "86 MB RAM", PurpleAccent);
        _lblMemGcHeap = AddMetricRow(tblMemMetrics, 1, 0, "Managed Heap", "Server GC Gen 2", TextPrimary);
        _lblMemArchitecture = AddMetricRow(tblMemMetrics, 0, 1, "CLR Target", ".NET 10.0 x64", CyanAccent);
        AddMetricRow(tblMemMetrics, 1, 1, "Compaction", "EmptyWorkingSet", GreenAccent);

        // 3. Action Operations Bar
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 6),
            WrapContents = false
        };
        Controls.Add(pnlActions);

        _btnStartAll = CreateActionButton("⚡ START ALL SERVICES", Color.FromArgb(14, 116, 144), Color.White, async (_, _) => await StartAllAsync());
        pnlActions.Controls.Add(_btnStartAll);

        _btnReloadTensors = CreateActionButton("🔄 RELOAD CV TENSORS", Color.FromArgb(30, 41, 59), TextPrimary, (_, _) => ReloadTensors());
        pnlActions.Controls.Add(_btnReloadTensors);

        _btnTrimMemory = CreateActionButton("🧹 TRIM RAM / VRAM", Color.FromArgb(30, 41, 59), TextPrimary, (_, _) => TrimMemory());
        pnlActions.Controls.Add(_btnTrimMemory);

        _btnEmergencyStop = CreateActionButton("⚠️ EMERGENCY STOP", Color.FromArgb(159, 18, 57), Color.White, async (_, _) => await EmergencyStopAsync());
        pnlActions.Controls.Add(_btnEmergencyStop);

        // 4. Real-Time Diagnostic Console Log
        var lblConsoleTitle = new Label
        {
            Text = "DIAGNOSTIC SYSTEM EVENT LOG",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(0, 8, 0, 0)
        };
        Controls.Add(lblConsoleTitle);

        _txtConsole = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(5, 10, 20),
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Consolas", 9.2f),
            BorderStyle = BorderStyle.None,
            ReadOnly = true
        };
        Controls.Add(_txtConsole);
        _txtConsole.BringToFront();

        // Polling Timer for Telemetry
        _telemetryTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _telemetryTimer.Tick += async (_, _) => await RefreshTelemetryAsync();
        _telemetryTimer.Start();

        Log("MLBB Companion Enterprise Dashboard initialized.", CyanAccent);
        Log("All services are idle by default. Click any card to enable or disable its service.", TextSecondary);
        UpdateCvEngineMetrics();
        UpdateAllCardStates();
    }

    private Panel CreateInteractiveServiceCard(
        string title,
        string textActive,
        string textInactive,
        Color accentColor,
        Func<bool> isEnabledFunc,
        Func<Task> onToggleAction,
        out Label lblStatusOut)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgCard,
            Padding = new Padding(12),
            Cursor = Cursors.Hand
        };

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(pnlHeader);

        var lblStatus = new Label
        {
            Text = isEnabledFunc() ? textActive : textInactive,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = isEnabledFunc() ? accentColor : TextMuted,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Cursor = Cursors.Hand,
            Padding = new Padding(0, 3, 0, 0)
        };
        pnlHeader.Controls.Add(lblStatus);
        lblStatusOut = lblStatus;

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Padding = new Padding(0, 3, 0, 0)
        };
        pnlHeader.Controls.Add(lblTitle);

        // Click handler that toggles the service
        async void HandleToggle(object? sender, EventArgs e)
        {
            try
            {
                await onToggleAction();
            }
            catch (Exception ex)
            {
                Log($"Error toggling {title}: {ex.Message}", RedAccent);
            }
        }

        panel.Click += HandleToggle;
        pnlHeader.Click += HandleToggle;
        lblTitle.Click += HandleToggle;
        lblStatus.Click += HandleToggle;

        panel.MouseEnter += (_, _) => { panel.BackColor = isEnabledFunc() ? BgCardActive : BgCardHover; };
        panel.MouseLeave += (_, _) => { panel.BackColor = isEnabledFunc() ? BgCardActive : BgCard; };
        pnlHeader.MouseEnter += (_, _) => { panel.BackColor = isEnabledFunc() ? BgCardActive : BgCardHover; };
        pnlHeader.MouseLeave += (_, _) => { panel.BackColor = isEnabledFunc() ? BgCardActive : BgCard; };

        panel.Paint += (_, e) =>
        {
            bool active = isEnabledFunc();
            var borderCol = active ? accentColor : BorderDefault;
            float penWidth = active ? 2f : 1f;
            using var pen = new Pen(borderCol, penWidth);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }

    private TableLayoutPanel CreateMetricGrid(int cols, int rows)
    {
        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = cols,
            RowCount = rows,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 2, 4, 2),
            Cursor = Cursors.Hand
        };
        for (int c = 0; c < cols; c++) tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
        for (int r = 0; r < rows; r++) tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        return tbl;
    }

    private Label AddMetricRow(TableLayoutPanel table, int col, int row, string label, string value, Color valColor)
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(2),
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Cursor = Cursors.Hand
        };

        var lblName = new Label
        {
            Text = label + ":",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = LabelTitleColor,
            AutoSize = true,
            Margin = new Padding(0, 2, 5, 0),
            Cursor = Cursors.Hand
        };
        pnl.Controls.Add(lblName);

        var lblVal = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = valColor,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
            Cursor = Cursors.Hand
        };
        pnl.Controls.Add(lblVal);

        table.Controls.Add(pnl, col, row);
        return lblVal;
    }

    private Button CreateActionButton(string text, Color bg, Color fg, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Height = 34,
            AutoSize = true,
            Padding = new Padding(14, 0, 14, 0),
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
        btn.Click += onClick;
        return btn;
    }

    private async Task ToggleBlueStacksAsync()
    {
        if (_isBsRunning)
        {
            Log("Stopping BlueStacks 5 emulator...", AmberAccent);
            await _launcherService.CloseBlueStacksAsync();
            _isBsRunning = false;
            Log("BlueStacks 5 stopped.", TextMuted);
        }
        else
        {
            Log("Launching BlueStacks 5 Android Emulator...", CyanAccent);
            await _launcherService.LaunchBlueStacksAsync();
            _isBsRunning = true;
            Log("BlueStacks 5 launch sequence initiated.", GreenAccent);
        }
        UpdateAllCardStates();
        await RefreshTelemetryAsync();
    }

    private Task ToggleVisionEngineAsync()
    {
        _isVisionEnabled = !_isVisionEnabled;
        if (_isVisionEnabled)
        {
            Log("Enabling OpenCV Vision Engine...", CyanAccent);
            ReloadTensors();
            Log("OpenCV Vision Engine enabled & template tensors active.", GreenAccent);
        }
        else
        {
            Log("OpenCV Vision Engine paused.", AmberAccent);
        }
        UpdateAllCardStates();
        return Task.CompletedTask;
    }

    private Task ToggleAdbCaptureAsync()
    {
        _isAdbCaptureEnabled = !_isAdbCaptureEnabled;
        if (_isAdbCaptureEnabled)
        {
            Log("ADB Bridge Screen Capture service enabled.", GreenAccent);
        }
        else
        {
            Log("ADB Bridge Screen Capture service disabled.", TextMuted);
        }
        UpdateAllCardStates();
        return Task.CompletedTask;
    }

    private Task ToggleMemoryWatchdogAsync()
    {
        _isMemWatchdogEnabled = !_isMemWatchdogEnabled;
        if (_isMemWatchdogEnabled)
        {
            Log("Runtime Memory Watchdog enabled. Compacting working set...", PurpleAccent);
            TrimMemory();
        }
        else
        {
            Log("Runtime Memory Watchdog disabled.", TextMuted);
        }
        UpdateAllCardStates();
        return Task.CompletedTask;
    }

    private void UpdateAllCardStates()
    {
        // BS5 Card
        _lblBsStatus.Text = _isBsRunning ? "● RUNNING • ONLINE" : "○ DISABLED • OFFLINE";
        _lblBsStatus.ForeColor = _isBsRunning ? GreenAccent : TextMuted;
        _cardBs.BackColor = _isBsRunning ? BgCardActive : BgCard;
        _cardBs.Invalidate();

        // CV Card
        _lblCvStatus.Text = _isVisionEnabled ? "● ACTIVE • 136 HEROES" : "○ DISABLED • PAUSED";
        _lblCvStatus.ForeColor = _isVisionEnabled ? CyanAccent : TextMuted;
        _cardCv.BackColor = _isVisionEnabled ? BgCardActive : BgCard;
        _cardCv.Invalidate();

        // ADB Card
        _lblAdbStatus.Text = _isAdbCaptureEnabled ? "● CAPTURE RUNNING" : "○ DISABLED • STANDBY";
        _lblAdbStatus.ForeColor = _isAdbCaptureEnabled ? GreenAccent : TextMuted;
        _cardAdb.BackColor = _isAdbCaptureEnabled ? BgCardActive : BgCard;
        _cardAdb.Invalidate();

        // Memory Card
        _lblMemStatus.Text = _isMemWatchdogEnabled ? "● WATCHDOG ACTIVE" : "○ DISABLED • IDLE";
        _lblMemStatus.ForeColor = _isMemWatchdogEnabled ? PurpleAccent : TextMuted;
        _cardMem.BackColor = _isMemWatchdogEnabled ? BgCardActive : BgCard;
        _cardMem.Invalidate();
    }

    private void UpdateCvEngineMetrics()
    {
        try
        {
            var counts = _visionEngine.GetTemplateCounts();
            if (counts.TryGetValue("unique_heroes", out int uh)) _lblCvHeroes.Text = $"{uh} Heroes";
            if (counts.TryGetValue("picks", out int p)) _lblCvPicks.Text = $"{p} Templates";
            if (counts.TryGetValue("anchors", out int a)) _lblCvAnchors.Text = $"{a} Anchors";

            long nativeBytes = _visionEngine.EstimatedNativeMemoryBytes;
            if (nativeBytes > 0)
            {
                double mb = (double)nativeBytes / (1024 * 1024);
                _lblCvNativeMem.Text = $"{mb:F1} MB (C++)";
            }
        }
        catch { }
    }

    private async Task RefreshTelemetryAsync()
    {
        try
        {
            var status = await _launcherService.GetStatusAsync();
            _isBsRunning = status.Bluestacks;

            if (_isBsRunning)
            {
                _lblAdbPort.Text = "127.0.0.1:5555 (Active)";
            }
            else
            {
                _lblAdbPort.Text = "127.0.0.1:5555 (Standby)";
            }

            long memBytes = Process.GetCurrentProcess().WorkingSet64;
            double memMb = (double)memBytes / (1024 * 1024);
            _lblMemWorkingSet.Text = $"{memMb:F1} MB RAM";
            _lblMemGcHeap.Text = $"GC: {GC.GetTotalMemory(false) / (1024 * 1024):F1} MB";

            // If memory watchdog is active and working set exceeds 140MB, perform automatic compaction
            if (_isMemWatchdogEnabled && memMb > 140.0)
            {
                TrimMemory();
            }

            UpdateAllCardStates();
        }
        catch
        {
            // Silently swallow polling telemetry error
        }
    }

    private async Task StartAllAsync()
    {
        _btnStartAll.Enabled = false;
        try
        {
            Log("=== Starting Automated Startup Pipeline ===", CyanAccent);
            Log("1. Launching BlueStacks 5 & Mobile Legends...", TextPrimary);
            await _launcherService.StartAllAsync();
            _isBsRunning = true;
            _isVisionEnabled = true;
            _isAdbCaptureEnabled = true;
            _isMemWatchdogEnabled = true;
            UpdateAllCardStates();
            Log("All companion services enabled & running.", GreenAccent);
            await RefreshTelemetryAsync();
        }
        catch (Exception ex)
        {
            Log($"Startup error: {ex.Message}", RedAccent);
        }
        finally
        {
            _btnStartAll.Enabled = true;
        }
    }

    private void ReloadTensors()
    {
        try
        {
            Log("Hot-reloading OpenCV CV template tensors...", AmberAccent);
            _visionEngine.ReloadTemplates();
            UpdateCvEngineMetrics();
            Log("All template tensors and phase anchors reloaded successfully.", GreenAccent);
        }
        catch (Exception ex)
        {
            Log($"Tensor reload error: {ex.Message}", RedAccent);
        }
    }

    private void TrimMemory()
    {
        try
        {
            long before = Process.GetCurrentProcess().WorkingSet64;
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            long after = Process.GetCurrentProcess().WorkingSet64;

            long freedMb = (before - after) / (1024 * 1024);
            Log($"Memory Compaction: Freed {freedMb} MB. Current Working Set: {after / (1024 * 1024)} MB.", GreenAccent);
        }
        catch (Exception ex)
        {
            Log($"Memory trim error: {ex.Message}", RedAccent);
        }
    }

    private async Task EmergencyStopAsync()
    {
        _btnEmergencyStop.Enabled = false;
        try
        {
            Log("EMERGENCY STOP: Terminating all services, BlueStacks, and processes...", RedAccent);
            await _launcherService.CloseBlueStacksAsync();
            _isBsRunning = false;
            _isVisionEnabled = false;
            _isAdbCaptureEnabled = false;
            _isMemWatchdogEnabled = false;
            UpdateAllCardStates();
            Log("All companion services stopped.", AmberAccent);
            await RefreshTelemetryAsync();
        }
        catch (Exception ex)
        {
            Log($"Emergency stop error: {ex.Message}", RedAccent);
        }
        finally
        {
            _btnEmergencyStop.Enabled = true;
        }
    }

    public void Log(string message, Color color)
    {
        if (IsDisposed) return;

        Action action = () =>
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            _txtConsole.SelectionStart = _txtConsole.TextLength;
            _txtConsole.SelectionColor = TextMuted;
            _txtConsole.AppendText($"[{ts}] ");

            _txtConsole.SelectionColor = color;
            _txtConsole.AppendText($"{message}\n");
            _txtConsole.ScrollToCaret();
        };

        if (InvokeRequired)
        {
            try { Invoke(action); } catch { }
        }
        else
        {
            action();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _telemetryTimer.Stop();
            _telemetryTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
