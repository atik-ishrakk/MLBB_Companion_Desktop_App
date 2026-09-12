using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using MLBBCompanion.BLL.Interfaces;
using MLBBCompanion.BLL.Models;
using MLBBCompanion.BLL.Services;
using MLBBCompanion.DAL.Implementations;
using MLBB.Infrastructure.Repositories;
using MLBB.Infrastructure.Screen;
using MLBB.Infrastructure.Vision;
using MLBBCompanion.GUI.Pages;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI;

public class MainForm : Form
{
    // Services
    private readonly ILauncherService _launcherService;
    private readonly IEnvironmentDetector _detector;
    private readonly IHeroDataService _heroDataService;
    private readonly IDraftRecommenderService _recommenderService;
    private readonly IVisionEngine _visionEngine;
    private readonly IScreenCaptureProvider _screenCapture;
    private readonly IRoiConfigurationService _roiConfig;
    private readonly IPhaseAnchorStorageService _phaseStorage;
    private readonly GuiDraftStateService _draftState;

    // Background Telemetry Timer
    private readonly System.Windows.Forms.Timer _pollTimer;

    // GUI Layout Controls
    private Panel _topNav = null!;
    private FlowLayoutPanel _tabsPanel = null!;
    private FlowLayoutPanel _actionsPanel = null!;
    private Button _btnPhaseStatus = null!;
    private Button _btnBsToggle = null!;
    private Button _btnCvTensor = null!;
    private Panel _pageHost = null!;

    // Native Windows Pages
    private DraftPickPage _draftPage = null!;
    private CheckerPage _checkerPage = null!;
    private EnterpriseHubPage _hubPage = null!;
    private UpdatesPage _updatesPage = null!;
    private Control _currentPage = null!;

    // Tabs tracking
    private readonly List<Button> _tabButtons = new();

    // Fullscreen state
    private bool _isFullscreen = false;
    private FormBorderStyle _prevBorderStyle;
    private FormWindowState _prevWindowState;
    private Rectangle _prevBounds;

    // Theme Colors
    private static readonly Color BgNav = Color.FromArgb(15, 23, 42);           // #0f172a
    private static readonly Color BgNavTab = Color.FromArgb(30, 41, 59);        // #1e293b
    private static readonly Color BgNavTabActive = Color.FromArgb(14, 116, 144); // #0e7490
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);   // #f8fafc
    private static readonly Color TextSecondary = Color.FromArgb(148, 163, 184); // #94a3b8
    private static readonly Color GreenAccent = Color.FromArgb(52, 211, 153);    // #34d399
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);     // #06b6d4
    private static readonly Color RedAccent = Color.FromArgb(190, 18, 60);       // #be123c

    public MainForm()
    {
        // 1. Initialize Complete 3-Tier Layer Services
        var runner = new ProcessRunner();
        _detector = new EnvironmentDetector();
        var adb = new AdbRepository(_detector, runner);
        var bridge = new WindowBridge();
        var config = new CompanionConfiguration { Port = 5000 };
        _launcherService = new LauncherService(adb, _detector, runner, bridge, NullLogger<LauncherService>.Instance, config);

        var heroRepo = new HeroRepository(NullLogger<HeroRepository>.Instance);
        _heroDataService = new HeroDataService(heroRepo);
        _recommenderService = new DraftRecommenderService(_heroDataService);

        var roiRepo = new RoiRepository(NullLogger<RoiRepository>.Instance);
        _roiConfig = new RoiConfigurationService(roiRepo);

        var serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        _phaseStorage = new PhaseAnchorStorageService(NullLogger<PhaseAnchorStorageService>.Instance, serviceProvider);
        _visionEngine = new OpenCvVisionEngine(NullLogger<OpenCvVisionEngine>.Instance, heroRepo);
        var adbService = new AdbService(NullLogger<AdbService>.Instance);
        _screenCapture = new ScreenCaptureProvider(NullLogger<ScreenCaptureProvider>.Instance, adbService);

        _draftState = new GuiDraftStateService(_heroDataService);

        // Pre-warm CV template tensors in background thread
        Task.Run(() =>
        {
            try { _visionEngine.InitializeTensors(); } catch { }
        });

        // 2. Configure Form Window
        Text = "MLBB Companion Enterprise Suite";
        Size = new Size(1380, 880);
        MinimumSize = new Size(1020, 680);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(10, 15, 29);
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;
        DoubleBuffered = true;

        // Set App Icon from Profile.png
        SetAppIcon();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        };

        // 3. Build Layout & Native Windows Pages
        BuildLayout();

        // 4. Background Polling Timer for Telemetry
        _pollTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _pollTimer.Tick += async (_, _) => await RefreshEmulatorStatusAsync();
        _pollTimer.Start();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void SetAppIcon()
    {
        try
        {
            string logoPath = Path.Combine(WorkspacePathResolver.GetWorkspaceRoot(), "Assets", "img", "Profile.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "img", "Profile.png");
            }
            if (File.Exists(logoPath))
            {
                using var bmp = new Bitmap(logoPath);
                using var iconBmp = new Bitmap(bmp, new Size(32, 32));
                IntPtr hIcon = iconBmp.GetHicon();
                Icon = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
            }
        }
        catch { }
    }

    private void BuildLayout()
    {
        // Top Navigation Bar
        _topNav = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = BgNav,
            Padding = new Padding(12, 6, 12, 6)
        };
        Controls.Add(_topNav);

        // Actions Flow Panel (Right Aligned: BlueStacks 5, CV Tensor — no icons)
        _actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft
        };
        _topNav.Controls.Add(_actionsPanel);

        // 1. CV Tensor Button
        _btnCvTensor = CreateActionButton("CV Tensor", BgNavTab, CyanAccent, (_, _) =>
        {
            try
            {
                _visionEngine.ReloadTemplates();
                _hubPage.Log("CV Tensors hot-reloaded from top navigation bar.", CyanAccent);
                MessageBox.Show("OpenCV Tensors and Phase Anchors reloaded successfully.", "CV Tensors", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed reloading tensors: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        _actionsPanel.Controls.Add(_btnCvTensor);

        // 2. BlueStacks 5 Toggle Button
        _btnBsToggle = CreateActionButton("BlueStacks 5", BgNavTab, TextPrimary, async (_, _) =>
        {
            _btnBsToggle.Enabled = false;
            try
            {
                var status = await _launcherService.GetStatusAsync();
                if (status.Bluestacks)
                {
                    await _launcherService.CloseBlueStacksAsync();
                }
                else
                {
                    await _launcherService.LaunchBlueStacksAsync();
                }
                await RefreshEmulatorStatusAsync();
            }
            finally
            {
                _btnBsToggle.Enabled = true;
            }
        });
        _actionsPanel.Controls.Add(_btnBsToggle);

        // 3. Phase Status Pill (top-right, reflects current draft phase)
        _btnPhaseStatus = new Button
        {
            Text = "● Phase: Draft Pick",
            Size = new Size(158, 28),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = CyanAccent,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 4, 0)
        };
        _btnPhaseStatus.FlatAppearance.BorderSize = 1;
        _btnPhaseStatus.FlatAppearance.BorderColor = CyanAccent;
        _btnPhaseStatus.Click += (_, _) =>
        {
            // Delegate phase cycling to the draft page
            if (_draftPage != null)
            {
                _draftPage.TriggerPhaseChange();
                _btnPhaseStatus.Text = $"● Phase: {_draftState.CurrentGamePhase}";
                UpdatePhaseStatusPill();
            }
        };
        _actionsPanel.Controls.Add(_btnPhaseStatus);

        // Keep Phase Status pill in sync whenever state changes
        _draftState.OnStateChanged += () =>
        {
            if (InvokeRequired)
                BeginInvoke(UpdatePhaseStatusPill);
            else
                UpdatePhaseStatusPill();
        };

        // Left Navigation Container: [Brand] strictly at the top-left, then [Tabs]
        var pnlNavLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _topNav.Controls.Add(pnlNavLeft);

        // Brand Area (Clickable to redirect to Enterprise Hub)
        var pnlBrand = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(0, 2, 8, 2)
        };
        pnlNavLeft.Controls.Add(pnlBrand);

        // Logo PictureBox
        string logoFile = Path.Combine(WorkspacePathResolver.GetWorkspaceRoot(), "Assets", "img", "Profile.png");
        if (!File.Exists(logoFile)) logoFile = Path.Combine(AppContext.BaseDirectory, "Assets", "img", "Profile.png");
        if (File.Exists(logoFile))
        {
            try
            {
                var picLogo = new PictureBox
                {
                    Size = new Size(32, 32),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = Image.FromFile(logoFile),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 8, 0)
                };
                picLogo.Click += (_, _) => ShowPage(_hubPage, null);
                pnlBrand.Controls.Add(picLogo);
            }
            catch { }
        }

        var lblBrand = new Label
        {
            Text = "MLBB COMPANION",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 0)
        };
        lblBrand.Click += (_, _) => ShowPage(_hubPage, null);
        pnlBrand.Controls.Add(lblBrand);
        pnlBrand.Click += (_, _) => ShowPage(_hubPage, null);

        // Tabs Flow Panel (Draft Pick, Vision Engine, Game Patches)
        _tabsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(4, 2, 0, 0),
            WrapContents = false
        };
        pnlNavLeft.Controls.Add(_tabsPanel);

        // Page Host Container (Fills entire form beneath nav bar)
        _pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 15, 29)
        };
        Controls.Add(_pageHost);
        _pageHost.BringToFront();

        // Create Native Windows Pages
        _draftPage = new DraftPickPage(_draftState, _heroDataService, _recommenderService, _visionEngine, _screenCapture, _roiConfig);
        _checkerPage = new CheckerPage(_visionEngine, _screenCapture, _roiConfig, _phaseStorage);
        _hubPage = new EnterpriseHubPage(_launcherService, _visionEngine);
        _updatesPage = new UpdatesPage();

        _pageHost.Controls.Add(_draftPage);
        _pageHost.Controls.Add(_checkerPage);
        _pageHost.Controls.Add(_hubPage);
        _pageHost.Controls.Add(_updatesPage);

        // Add Native Page Tabs (Draft Pick, Vision Engine, Game Patches)
        AddPageTab("Draft Pick", _draftPage);
        AddPageTab("Vision Engine", _checkerPage);
        AddPageTab("Game Patches", _updatesPage);

        // Set Default Active Landing Page -> Enterprise Hub
        ShowPage(_hubPage, null);
    }

    private void AddPageTab(string text, Control pageControl)
    {
        var btn = new Button
        {
            Text = text,
            Tag = pageControl,
            BackColor = BgNavTab,
            ForeColor = TextSecondary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Height = 32,
            AutoSize = true,
            Padding = new Padding(14, 0, 14, 0),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => ShowPage(pageControl, btn);

        _tabButtons.Add(btn);
        _tabsPanel.Controls.Add(btn);
    }

    private void ShowPage(Control pageControl, Button? activeBtn)
    {
        _currentPage = pageControl;

        foreach (var b in _tabButtons)
        {
            bool isActive = (b == activeBtn);
            b.BackColor = isActive ? BgNavTabActive : BgNavTab;
            b.ForeColor = isActive ? Color.White : TextSecondary;
            b.Font = new Font("Segoe UI", 9f, isActive ? FontStyle.Bold : FontStyle.Regular);
        }

        foreach (Control c in _pageHost.Controls)
        {
            c.Visible = (c == pageControl);
        }

        pageControl.BringToFront();
    }

    private Button CreateActionButton(string text, Color bg, Color fg, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Height = 32,
            AutoSize = true,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(6, 2, 0, 2),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _prevBorderStyle = FormBorderStyle;
            _prevWindowState = WindowState;
            _prevBounds = Bounds;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            _isFullscreen = true;
        }
        else
        {
            FormBorderStyle = _prevBorderStyle;
            WindowState = _prevWindowState;
            Bounds = _prevBounds;
            _isFullscreen = false;
        }
    }

    private async Task RefreshEmulatorStatusAsync()
    {
        try
        {
            var status = await _launcherService.GetStatusAsync();

            Action update = () =>
            {
                if (status.Bluestacks)
                {
                    _btnBsToggle.Text = status.GameRunning ? "MLBB Live" : "BS5 Online";
                    _btnBsToggle.ForeColor = GreenAccent;
                }
                else
                {
                    _btnBsToggle.Text = "BlueStacks 5";
                    _btnBsToggle.ForeColor = TextPrimary;
                }
            };

            if (InvokeRequired)
            {
                try { Invoke(update); } catch { }
            }
            else
            {
                update();
            }
        }
        catch
        {
            // Ignore polling errors
        }
    }

    private void UpdatePhaseStatusPill()
    {
        var phase = _draftState.CurrentGamePhase;
        _btnPhaseStatus.Text = $"● Phase: {phase}";

        // Color the pill by phase type
        var (fg, border) = phase switch
        {
            "Draft Pick" or "Ban Phase" or "Pick Phase" => (CyanAccent, CyanAccent),
            "In-Game Laning" => (GreenAccent, GreenAccent),
            _ => (TextSecondary, Color.FromArgb(51, 65, 85))
        };
        _btnPhaseStatus.ForeColor = fg;
        _btnPhaseStatus.FlatAppearance.BorderColor = border;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _pollTimer.Stop();
        try
        {
            // Terminate background emulator and game processes cleanly on window close
            var closeTask = _launcherService.CloseBlueStacksAsync();
            closeTask.Wait(2000);
        }
        catch { }
        try
        {
            var stopTask = _launcherService.StopGameAsync();
            stopTask.Wait(1000);
        }
        catch { }
        try
        {
            _visionEngine.Dispose();
        }
        catch { }
        base.OnFormClosing(e);
    }
}
