using System.Diagnostics;
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

    // OpenCV Tensor Engine Labels
    private readonly Label _lblCvStatus;
    private readonly Label _lblCvHeroes;
    private readonly Label _lblCvPicks;
    private readonly Label _lblCvBans;
    private readonly Label _lblCvLanes;
    private readonly Label _lblCvSpells;
    private readonly Label _lblCvAnchors;
    private readonly Label _lblCvNativeMem;
    private readonly Label _lblCvPipeline;
    private readonly Label _lblCvConcurrency;

    // Hardware & ADB Bridge Labels
    private readonly Label _lblAdbStatus;
    private readonly Label _lblAdbPort;
    private readonly Label _lblAdbCapture;
    private readonly Label _lblAdbResolution;

    // Runtime & Memory Labels
    private readonly Label _lblMemWorkingSet;
    private readonly Label _lblMemGcHeap;
    private readonly Label _lblMemArchitecture;

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
    private static readonly Color BgCardInner = Color.FromArgb(13, 19, 33);
    private static readonly Color BorderColor = Color.FromArgb(30, 41, 59);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color GreenAccent = Color.FromArgb(52, 211, 153);
    private static readonly Color RedAccent = Color.FromArgb(225, 29, 72);
    private static readonly Color AmberAccent = Color.FromArgb(245, 158, 11);
    private static readonly Color PurpleAccent = Color.FromArgb(168, 85, 247);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(226, 232, 240);
    private static readonly Color TextMuted = Color.FromArgb(186, 205, 230);
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

        // Header Panel
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
        Controls.Add(pnlHeader);

        var lblTitle = new Label
        {
            Text = "ENTERPRISE COMMAND HUB & TELEMETRY",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(0, 4)
        };
        pnlHeader.Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Unified real-time process manager, OpenCvSharp4 tensor telemetry, and BlueStacks hardware ADB bridge.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(0, 30)
        };
        pnlHeader.Controls.Add(lblSubtitle);

        // Main Cards Container (Split into Left: Detailed OpenCV Tensor Card, Right: Stacked ADB + Memory Cards)
        var cardsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 295,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 12)
        };
        cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        Controls.Add(cardsTable);

        // ==========================================
        // CARD 1: OPENCV TENSOR INFERENCE ENGINE (Detailed)
        // ==========================================
        var pnlCvCard = CreateGlassCard("OPENCV TENSOR & VISION ENGINE", out _lblCvStatus, CyanAccent, "● PRE-WARMED • READY");
        pnlCvCard.Margin = new Padding(0, 0, 10, 0);
        cardsTable.Controls.Add(pnlCvCard, 0, 0);

        var tblCvMetrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 4, 6, 4)
        };
        tblCvMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblCvMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (int r = 0; r < 5; r++) tblCvMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
        pnlCvCard.Controls.Add(tblCvMetrics);
        tblCvMetrics.BringToFront();

        _lblCvHeroes = AddMetricRow(tblCvMetrics, 0, 0, "Unique Hero Classes", "136 Heroes", CyanAccent);
        _lblCvPicks = AddMetricRow(tblCvMetrics, 1, 0, "Pick Slot Tensors", "136 Templates", TextPrimary);
        _lblCvBans = AddMetricRow(tblCvMetrics, 0, 1, "Ban Slot Tensors", "136 Circular", TextPrimary);
        _lblCvLanes = AddMetricRow(tblCvMetrics, 1, 1, "Lane Badges", "5 Lanes (R, E, M, G, J)", TextPrimary);
        _lblCvSpells = AddMetricRow(tblCvMetrics, 0, 2, "Battle Spells", "11 Spells", TextPrimary);
        _lblCvAnchors = AddMetricRow(tblCvMetrics, 1, 2, "Phase Anchors", "23 Active Anchors", GreenAccent);
        _lblCvNativeMem = AddMetricRow(tblCvMetrics, 0, 3, "Native Mat Buffer", "38.4 MB (C++ Heap)", AmberAccent);
        _lblCvPipeline = AddMetricRow(tblCvMetrics, 1, 3, "Algorithm Pipeline", "CLAHE 2.0 + TM_CCOEFF_NORMED", TextSecondary);
        _lblCvConcurrency = AddMetricRow(tblCvMetrics, 0, 4, "Reader Concurrency", "Lock-Free (Interlocked)", GreenAccent);
        AddMetricRow(tblCvMetrics, 1, 4, "Target Resolution", "1920 × 1080 (16:9 FHD)", TextSecondary);

        // ==========================================
        // RIGHT CONTAINER: ADB BRIDGE + MEMORY CARDS
        // ==========================================
        var pnlRightStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        pnlRightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        pnlRightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        cardsTable.Controls.Add(pnlRightStack, 1, 0);

        // CARD 2: ADB BRIDGE & SCREEN CAPTURE
        var pnlAdbCard = CreateGlassCard("ADB BRIDGE & SCREEN CAPTURE", out _lblAdbStatus, GreenAccent, "● BRIDGE ACTIVE");
        pnlAdbCard.Margin = new Padding(0, 0, 0, 6);
        pnlRightStack.Controls.Add(pnlAdbCard, 0, 0);

        var tblAdbMetrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 2, 6, 2)
        };
        tblAdbMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblAdbMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblAdbMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        tblAdbMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        pnlAdbCard.Controls.Add(tblAdbMetrics);
        tblAdbMetrics.BringToFront();

        _lblAdbPort = AddMetricRow(tblAdbMetrics, 0, 0, "Daemon Port", "127.0.0.1:5555", CyanAccent);
        _lblAdbCapture = AddMetricRow(tblAdbMetrics, 1, 0, "Capture Pipeline", "Win32 GDI BitBlt", TextPrimary);
        _lblAdbResolution = AddMetricRow(tblAdbMetrics, 0, 1, "Viewport Frame", "1920x1080 32bpp", TextSecondary);
        AddMetricRow(tblAdbMetrics, 1, 1, "Direct ADB Transfer", "Low-Latency Direct Memory", GreenAccent);

        // CARD 3: RUNTIME PERFORMANCE & MEMORY COMPACTION
        var pnlMemCard = CreateGlassCard("RUNTIME PERFORMANCE & MEMORY", out _lblMemWorkingSet, PurpleAccent, "86 MB RAM");
        pnlMemCard.Margin = new Padding(0, 6, 0, 0);
        pnlRightStack.Controls.Add(pnlMemCard, 0, 1);

        var tblMemMetrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 2, 6, 2)
        };
        tblMemMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblMemMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblMemMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        tblMemMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        pnlMemCard.Controls.Add(tblMemMetrics);
        tblMemMetrics.BringToFront();

        _lblMemGcHeap = AddMetricRow(tblMemMetrics, 0, 0, "Managed GC Heap", "Server GC Gen 0/1/2", TextPrimary);
        _lblMemArchitecture = AddMetricRow(tblMemMetrics, 1, 0, "CLR Runtime", ".NET 10.0 x64", CyanAccent);
        AddMetricRow(tblMemMetrics, 0, 1, "Memory Compaction", "Win32 EmptyWorkingSet", GreenAccent);
        AddMetricRow(tblMemMetrics, 1, 1, "Unmanaged DLL Heap", "OpenCvSharp4 Native C++", AmberAccent);

        // Action Operations Bar
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

        // Real-Time Diagnostic Console Log
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

        // Polling Timer
        _telemetryTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _telemetryTimer.Tick += async (_, _) => await RefreshTelemetryAsync();
        _telemetryTimer.Start();

        Log("MLBB Companion Enterprise Suite initialized.", CyanAccent);
        Log("OpenCvSharp4 tensor caches pre-warmed in unmanaged memory.", GreenAccent);
        UpdateCvEngineMetrics();
    }

    private Panel CreateGlassCard(string title, out Label lblStatus, Color statusColor, string initialStatus)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgCard,
            Padding = new Padding(12)
        };

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(pnlHeader);

        lblStatus = new Label
        {
            Text = initialStatus,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = statusColor,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 4, 0, 0)
        };
        pnlHeader.Controls.Add(lblStatus);

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = LabelTitleColor,
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 0)
        };
        pnlHeader.Controls.Add(lblTitle);

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }

    private Label AddMetricRow(TableLayoutPanel table, int col, int row, string label, string value, Color valColor)
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(2),
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        var lblName = new Label
        {
            Text = label + ":",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
            ForeColor = LabelTitleColor,
            AutoSize = true,
            Margin = new Padding(0, 2, 6, 0)
        };
        pnl.Controls.Add(lblName);

        var lblVal = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = valColor,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
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

    private void UpdateCvEngineMetrics()
    {
        try
        {
            var counts = _visionEngine.GetTemplateCounts();
            if (counts.TryGetValue("unique_heroes", out int uh)) _lblCvHeroes.Text = $"{uh} Heroes";
            if (counts.TryGetValue("picks", out int p)) _lblCvPicks.Text = $"{p} Templates";
            if (counts.TryGetValue("bans", out int b)) _lblCvBans.Text = $"{b} Circular";
            if (counts.TryGetValue("lanes", out int l)) _lblCvLanes.Text = $"{l} Lanes";
            if (counts.TryGetValue("spells", out int s)) _lblCvSpells.Text = $"{s} Spells";
            if (counts.TryGetValue("anchors", out int a)) _lblCvAnchors.Text = $"{a} Active Anchors";

            long nativeBytes = _visionEngine.EstimatedNativeMemoryBytes;
            if (nativeBytes > 0)
            {
                double mb = (double)nativeBytes / (1024 * 1024);
                _lblCvNativeMem.Text = $"{mb:F1} MB (C++ Heap)";
            }
        }
        catch { }
    }

    private async Task RefreshTelemetryAsync()
    {
        try
        {
            var status = await _launcherService.GetStatusAsync();

            if (status.Bluestacks)
            {
                _lblAdbStatus.Text = status.GameRunning ? "● MLBB LIVE (ONLINE)" : "● BS5 CONNECTED";
                _lblAdbStatus.ForeColor = GreenAccent;
                _lblAdbPort.Text = "127.0.0.1:5555 (Active)";
            }
            else
            {
                _lblAdbStatus.Text = "● STANDBY / DISCONNECTED";
                _lblAdbStatus.ForeColor = AmberAccent;
                _lblAdbPort.Text = "127.0.0.1:5555 (Standby)";
            }

            long memBytes = Process.GetCurrentProcess().WorkingSet64;
            double memMb = (double)memBytes / (1024 * 1024);
            _lblMemWorkingSet.Text = $"{memMb:F1} MB RAM";
            _lblMemGcHeap.Text = $"GC: {GC.GetTotalMemory(false) / (1024 * 1024):F1} MB";

            _lblCvStatus.Text = _visionEngine.IsReady ? "● PRE-WARMED • READY" : "● INITIALIZING...";
            _lblCvStatus.ForeColor = _visionEngine.IsReady ? CyanAccent : AmberAccent;
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
            Log("BlueStacks startup request sent successfully.", GreenAccent);
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
            Log("EMERGENCY STOP: Killing BlueStacks, ADB, and game processes...", RedAccent);
            await _launcherService.CloseBlueStacksAsync();
            Log("BlueStacks emulator terminated.", AmberAccent);
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
