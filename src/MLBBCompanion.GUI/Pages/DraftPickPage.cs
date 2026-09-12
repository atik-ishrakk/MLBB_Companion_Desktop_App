using System.Drawing.Drawing2D;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBBCompanion.GUI.Controls;
using MLBBCompanion.GUI.Dialogs;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Pages;

public class DraftPickPage : UserControl
{
    private readonly GuiDraftStateService _draftState;
    private readonly IHeroDataService _heroDataService;
    private readonly IDraftRecommenderService _recommenderService;
    private readonly IVisionEngine _visionEngine;
    private readonly IScreenCaptureProvider _screenCapture;
    private readonly IRoiConfigurationService _roiConfig;

    // Controls - Top Bar
    private readonly Button[] _btnBans = new Button[3];
    private readonly Button _btnCvSync;
    private readonly Button _btnReset;

    // Controls - 5v5 Board
    private readonly FlowLayoutPanel _pnlAllyBans;
    private readonly FlowLayoutPanel _pnlEnemyBans;
    private readonly List<BanSlotControl> _allyBanSlots = new();
    private readonly List<BanSlotControl> _enemyBanSlots = new();
    private readonly PickSlotControl[] _allyPickSlots = new PickSlotControl[5];
    private readonly PickSlotControl[] _enemyPickSlots = new PickSlotControl[5];

    // Controls - Strategy Sidebar
    private readonly Label _lblAllyWinProb;
    private readonly Label _lblEnemyWinProb;
    private readonly WinProbabilityBar _winBar;
    private readonly Label _lblDefense;
    private readonly Label _lblCc;
    private readonly Label _lblDmgSplit;
    private readonly Label _lblWinExplanation;
    private readonly FlowLayoutPanel _pnlThreats;
    private readonly FlowLayoutPanel _pnlRecommendedHeroes;
    private readonly FlowLayoutPanel _pnlRecommendedItems;

    // Background CV Timer
    private readonly System.Windows.Forms.Timer _cvSyncTimer;
    private bool _isScanning = false;

    // Theme Colors
    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);           // #0a0f1d
    private static readonly Color BgCard = Color.FromArgb(20, 29, 47);           // #141d2f
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);       // #334155
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);      // #06b6d4
    private static readonly Color RedAccent = Color.FromArgb(225, 29, 72);        // #e11d48
    private static readonly Color GoldAccent = Color.FromArgb(245, 158, 11);      // #f59e0b
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public DraftPickPage(
        GuiDraftStateService draftState,
        IHeroDataService heroDataService,
        IDraftRecommenderService recommenderService,
        IVisionEngine visionEngine,
        IScreenCaptureProvider screenCapture,
        IRoiConfigurationService roiConfig)
    {
        _draftState = draftState;
        _heroDataService = heroDataService;
        _recommenderService = recommenderService;
        _visionEngine = visionEngine;
        _screenCapture = screenCapture;
        _roiConfig = roiConfig;

        Dock = DockStyle.Fill;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;

        // 1. Top Sub-Navigation Toolbar
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = BgDark,
            Padding = new Padding(12, 8, 12, 8)
        };
        Controls.Add(topBar);

        // LEFT: CV Sync Toggle Button
        _btnCvSync = new Button
        {
            Text = "⚡ AUTO-CV SYNC: OFF",
            Size = new Size(165, 30),
            Location = new Point(14, 9),
            BackColor = BgCard,
            ForeColor = TextMuted,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnCvSync.FlatAppearance.BorderSize = 1;
        _btnCvSync.FlatAppearance.BorderColor = BorderColor;
        _btnCvSync.Click += (_, _) => ToggleCvSync();
        topBar.Controls.Add(_btnCvSync);

        // CENTER: Ban Format Container
        var pnlBanFormat = new Panel
        {
            Size = new Size(330, 32),
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(pnlBanFormat);

        var lblBanFmt = new Label
        {
            Text = "BAN FORMAT:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(0, 7)
        };
        pnlBanFormat.Controls.Add(lblBanFmt);

        int[] banCounts = [3, 4, 5];
        for (int i = 0; i < 3; i++)
        {
            int count = banCounts[i];
            var btn = new Button
            {
                Text = $"{count} BANS",
                Tag = count,
                Size = new Size(68, 28),
                Location = new Point(104 + (i * 74), 2),
                BackColor = count == _draftState.MaxBans ? CyanAccent : BgCard,
                ForeColor = count == _draftState.MaxBans ? Color.Black : TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) =>
            {
                _draftState.SetBanFormat((int)btn.Tag!);
                UpdateBanFormatButtons();
                RebuildBanSlots();
            };
            _btnBans[i] = btn;
            pnlBanFormat.Controls.Add(btn);
        }

        // RIGHT: Reset Button on Top-Right Corner
        _btnReset = new Button
        {
            Text = "🔄 RESET DRAFT",
            Size = new Size(125, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(topBar.Width - 140, 9),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnReset.FlatAppearance.BorderSize = 0;
        _btnReset.Click += (_, _) => _draftState.ResetDraft();
        topBar.Controls.Add(_btnReset);

        topBar.Resize += (_, _) =>
        {
            pnlBanFormat.Location = new Point(Math.Max(185, (topBar.Width - pnlBanFormat.Width) / 2), 8);
            _btnReset.Location = new Point(topBar.Width - 140, 9);
        };
        pnlBanFormat.Location = new Point(Math.Max(185, (topBar.Width - pnlBanFormat.Width) / 2), 8);

        // 2. Main Workspace Split (50% Left 5v5 Board, 50% Right Strategy Hub)
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        Controls.Add(mainSplit);
        mainSplit.BringToFront();

        void AdjustSplitter50Percent()
        {
            if (mainSplit.Width > 200)
            {
                try { mainSplit.SplitterDistance = mainSplit.Width / 2; } catch { }
            }
        }

        mainSplit.Resize += (_, _) => AdjustSplitter50Percent();
        Load += (_, _) => AdjustSplitter50Percent();

        // LEFT PANEL: 5v5 TEAMS
        var pnlTeams = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        mainSplit.Panel1.Controls.Add(pnlTeams);

        var teamsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        teamsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        teamsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pnlTeams.Controls.Add(teamsTable);

        // Ally Column (FlowLayoutPanel with Header -> Bans -> Picks)
        var colAlly = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(4),
            BackColor = Color.Transparent
        };
        teamsTable.Controls.Add(colAlly, 0, 0);

        var lblAllyHeader = new Label
        {
            Text = "BLUE ALLY TEAM",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Size = new Size(380, 26),
            Margin = new Padding(4, 2, 0, 2)
        };
        colAlly.Controls.Add(lblAllyHeader);

        _pnlAllyBans = new FlowLayoutPanel
        {
            Size = new Size(380, 58),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6),
            WrapContents = false
        };
        colAlly.Controls.Add(_pnlAllyBans);

        var pnlAllyPicks = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        colAlly.Controls.Add(pnlAllyPicks);

        for (int i = 0; i < 5; i++)
        {
            var pickSlot = new PickSlotControl(i, isEnemy: false);
            pickSlot.Margin = new Padding(0, 0, 0, 8);
            WirePickSlotEvents(pickSlot);
            _allyPickSlots[i] = pickSlot;
            pnlAllyPicks.Controls.Add(pickSlot);
        }

        // Enemy Column (FlowLayoutPanel with Header -> Bans -> Picks)
        var colEnemy = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(4),
            BackColor = Color.Transparent
        };
        teamsTable.Controls.Add(colEnemy, 1, 0);

        var lblEnemyHeader = new Label
        {
            Text = "RED ENEMY TEAM",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = RedAccent,
            Size = new Size(380, 26),
            Margin = new Padding(4, 2, 0, 2)
        };
        colEnemy.Controls.Add(lblEnemyHeader);

        _pnlEnemyBans = new FlowLayoutPanel
        {
            Size = new Size(380, 58),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6),
            WrapContents = false
        };
        colEnemy.Controls.Add(_pnlEnemyBans);

        var pnlEnemyPicks = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        colEnemy.Controls.Add(pnlEnemyPicks);

        for (int i = 0; i < 5; i++)
        {
            var pickSlot = new PickSlotControl(i, isEnemy: true);
            pickSlot.Margin = new Padding(0, 0, 0, 8);
            WirePickSlotEvents(pickSlot);
            _enemyPickSlots[i] = pickSlot;
            pnlEnemyPicks.Controls.Add(pickSlot);
        }

        colAlly.Resize += (_, _) =>
        {
            lblAllyHeader.Width = Math.Max(280, colAlly.Width - 10);
            _pnlAllyBans.Width = Math.Max(280, colAlly.Width - 10);
            lblEnemyHeader.Width = Math.Max(280, colEnemy.Width - 10);
            _pnlEnemyBans.Width = Math.Max(280, colEnemy.Width - 10);
        };

        // RIGHT PANEL: STRATEGY HUB SIDEBAR
        var pnlSidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 20)
        };
        mainSplit.Panel2.Controls.Add(pnlSidebar);

        var lblStrategyTitle = new Label
        {
            Text = "STRATEGY ENGINE",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Top,
            Height = 32
        };
        pnlSidebar.Controls.Add(lblStrategyTitle);

        // 1. Win Probability Card
        var cardWin = CreateSidebarCard("WIN PROBABILITY & SYNERGY", pnlSidebar);

        var winHeaderRow = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
        cardWin.Controls.Add(winHeaderRow);

        _lblAllyWinProb = new Label
        {
            Text = "BLUE ALLY 50%",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Left,
            AutoSize = true
        };
        winHeaderRow.Controls.Add(_lblAllyWinProb);

        _lblEnemyWinProb = new Label
        {
            Text = "50% RED ENEMY",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = RedAccent,
            Dock = DockStyle.Right,
            AutoSize = true
        };
        winHeaderRow.Controls.Add(_lblEnemyWinProb);

        _winBar = new WinProbabilityBar { Dock = DockStyle.Top, Height = 18, Margin = new Padding(0, 4, 0, 8) };
        cardWin.Controls.Add(_winBar);

        // Metrics pills row
        var metricsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Margin = new Padding(0, 8, 0, 4),
            BackColor = Color.Transparent,
            WrapContents = false
        };
        cardWin.Controls.Add(metricsRow);

        _lblDefense = CreateMetricPill("DEFENSE: 50", metricsRow);
        _lblCc = CreateMetricPill("CC CONTROL: 50", metricsRow);
        _lblDmgSplit = CreateMetricPill("DMG: 50/50", metricsRow);

        _lblWinExplanation = new Label
        {
            Text = "Draft is balanced. BlueStacks CV auto-synchronizes locked picks and bans in real time.",
            Font = new Font("Segoe UI", 8.2f),
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        cardWin.Controls.Add(_lblWinExplanation);

        // 2. Threat Composition Card
        var cardThreats = CreateSidebarCard("THREAT COMPOSITION MATRIX", pnlSidebar, GoldAccent);
        _pnlThreats = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        cardThreats.Controls.Add(_pnlThreats);

        // 3. Counter-Pick Recommendations Card
        var cardRec = CreateSidebarCard("TACTICAL COUNTER-PICKS", pnlSidebar, CyanAccent);
        _pnlRecommendedHeroes = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        cardRec.Controls.Add(_pnlRecommendedHeroes);

        // 4. Situational Counter Items Card
        var cardItems = CreateSidebarCard("RECOMMENDED EQUIPMENT", pnlSidebar, GoldAccent);
        _pnlRecommendedItems = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        cardItems.Controls.Add(_pnlRecommendedItems);

        // Background CV Poller
        _cvSyncTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _cvSyncTimer.Tick += async (_, _) => await RunCvSyncCycleAsync();

        // Listen to live detection results to update draft panel slots in real time
        _visionEngine.OnDraftDetectionUpdated += HandleDraftDetectionUpdated;

        // Wire State & Populate
        _draftState.OnStateChanged += SyncFromState;
        RebuildBanSlots();
        SyncFromState();
    }

    private Panel CreateSidebarCard(string title, Panel parent, Color? headerColor = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = BgCard,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 12)
        };

        var lbl = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = headerColor ?? CyanAccent,
            Dock = DockStyle.Top,
            Height = 24
        };
        card.Controls.Add(lbl);

        card.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        parent.Controls.Add(card);
        card.BringToFront();
        return card;
    }

    private Label CreateMetricPill(string text, FlowLayoutPanel parent)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(0, 0, 6, 0),
            AutoSize = true
        };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private void RebuildBanSlots()
    {
        _pnlAllyBans.SuspendLayout();
        _pnlEnemyBans.SuspendLayout();
        _pnlAllyBans.Controls.Clear();
        _pnlEnemyBans.Controls.Clear();
        _allyBanSlots.Clear();
        _enemyBanSlots.Clear();

        for (int i = 0; i < _draftState.MaxBans; i++)
        {
            var allyBan = new BanSlotControl(i, isEnemy: false) { Margin = new Padding(0, 0, 6, 0) };
            allyBan.BanSlotClicked += OnBanSlotClicked;
            _allyBanSlots.Add(allyBan);
            _pnlAllyBans.Controls.Add(allyBan);

            var enemyBan = new BanSlotControl(i, isEnemy: true) { Margin = new Padding(0, 0, 6, 0) };
            enemyBan.BanSlotClicked += OnBanSlotClicked;
            _enemyBanSlots.Add(enemyBan);
            _pnlEnemyBans.Controls.Add(enemyBan);
        }

        _pnlAllyBans.ResumeLayout(true);
        _pnlEnemyBans.ResumeLayout(true);
    }

    private void UpdateBanFormatButtons()
    {
        foreach (var b in _btnBans)
        {
            bool active = (int)b.Tag! == _draftState.MaxBans;
            b.BackColor = active ? CyanAccent : BgCard;
            b.ForeColor = active ? Color.Black : TextPrimary;
        }
    }

    private void WirePickSlotEvents(PickSlotControl slot)
    {
        slot.CardSelected += (idx, isEnemy) =>
        {
            _draftState.SelectedSide = isEnemy ? "enemy" : "ally";
            _draftState.SelectedSlotIndex = idx;
            if (!isEnemy) _draftState.MyHeroIndex = idx;
            SyncFromState();
        };

        slot.HeroAvatarClicked += (idx, isEnemy) =>
        {
            using var dlg = new HeroSelectDialog(_heroDataService, isEnemy ? $"SELECT ENEMY HERO (SLOT {idx + 1})" : $"SELECT ALLY HERO (SLOT {idx + 1})");
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _draftState.SelectHero(isEnemy ? "enemy" : "ally", idx, dlg.SelectedHero);
            }
        };

        slot.LaneClicked += (idx, isEnemy) =>
        {
            string[] lanes = ["EXP", "Jungle", "Mid", "Gold", "Roam"];
            var current = isEnemy ? _draftState.EnemyLanes[idx] : _draftState.AllyLanes[idx];
            int next = (Array.IndexOf(lanes, current) + 1) % lanes.Length;
            _draftState.SetLane(isEnemy ? "enemy" : "ally", idx, lanes[next]);
        };

        slot.SpellClicked += (idx, isEnemy) =>
        {
            using var dlg = new SpellSelectDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedSpellId))
            {
                _draftState.SetSpell(isEnemy ? "enemy" : "ally", idx, dlg.SelectedSpellId);
            }
        };

        slot.ItemSlotClicked += (slotIdx, itemIdx, isEnemy) =>
        {
            using var dlg = new ItemSelectDialog(_heroDataService);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _draftState.SetItem(isEnemy ? "enemy" : "ally", slotIdx, itemIdx, dlg.SelectedItem);
            }
        };

        slot.ClearClicked += (idx, isEnemy) =>
        {
            _draftState.ClearSlot(isEnemy ? "enemy" : "ally", idx);
        };
    }

    private void OnBanSlotClicked(int slotIndex, bool isEnemy)
    {
        using var dlg = new HeroSelectDialog(_heroDataService, isEnemy ? $"BAN ENEMY HERO (SLOT {slotIndex + 1})" : $"BAN ALLY HERO (SLOT {slotIndex + 1})");
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _draftState.SelectHero(isEnemy ? "enemy" : "ally", slotIndex, dlg.SelectedHero, isBan: true);
        }
    }

    private void SyncFromState()
    {
        // 1. Sync Ban Slots
        for (int i = 0; i < _allyBanSlots.Count; i++)
        {
            _allyBanSlots[i].Hero = (i < _draftState.AllyBans.Count) ? _draftState.AllyBans[i] : null;
        }
        for (int i = 0; i < _enemyBanSlots.Count; i++)
        {
            _enemyBanSlots[i].Hero = (i < _draftState.EnemyBans.Count) ? _draftState.EnemyBans[i] : null;
        }

        // 2. Sync Pick Slots
        for (int i = 0; i < 5; i++)
        {
            _allyPickSlots[i].Hero = _draftState.AllyPicks[i];
            _allyPickSlots[i].Lane = _draftState.AllyLanes[i];
            _allyPickSlots[i].Spell = _draftState.AllySpells[i];
            _allyPickSlots[i].IsSelected = (_draftState.SelectedSide == "ally" && _draftState.SelectedSlotIndex == i);
            _allyPickSlots[i].IsMyHero = (_draftState.MyHeroIndex == i);

            for (int k = 0; k < 6; k++)
            {
                _allyPickSlots[i].SetItem(k, _draftState.AllyEquipments[i][k]);
            }

            _enemyPickSlots[i].Hero = _draftState.EnemyPicks[i];
            _enemyPickSlots[i].Lane = _draftState.EnemyLanes[i];
            _enemyPickSlots[i].Spell = _draftState.EnemySpells[i];
            _enemyPickSlots[i].IsSelected = (_draftState.SelectedSide == "enemy" && _draftState.SelectedSlotIndex == i);

            for (int k = 0; k < 6; k++)
            {
                _enemyPickSlots[i].SetItem(k, _draftState.EnemyEquipments[i][k]);
            }
        }

        // 3. Run Strategy Calculation
        UpdateStrategyEngine();
    }

    private void UpdateStrategyEngine()
    {
        var allyHeroIds = _draftState.AllyPicks.Where(h => h != null).Select(h => h!.Id).ToList();
        var enemyHeroIds = _draftState.EnemyPicks.Where(h => h != null).Select(h => h!.Id).ToList();
        var allBannedIds = _draftState.AllyBans.Concat(_draftState.EnemyBans).Where(h => h != null).Select(h => h!.Id).ToList();

        var request = new DraftRecommendationRequest
        {
            AllyPicks = allyHeroIds,
            EnemyPicks = enemyHeroIds,
            AllyBans = _draftState.AllyBans.Where(h => h != null).Select(h => h!.Id).ToList(),
            EnemyBans = _draftState.EnemyBans.Where(h => h != null).Select(h => h!.Id).ToList()
        };

        var result = _recommenderService.AnalyzeDraft(request);

        // Win Probabilities
        _lblAllyWinProb.Text = $"BLUE ALLY {result.AllyWinProbability:F0}%";
        _lblEnemyWinProb.Text = $"{result.EnemyWinProbability:F0}% RED ENEMY";
        _winBar.AllyWinProbability = result.AllyWinProbability;

        // Metrics Pills
        var comp = result.AllyComposition ?? new TeamCompositionAnalysis();
        _lblDefense.Text = $"DEFENSE: {comp.DurabilityScore:F0}";
        _lblCc.Text = $"CC CONTROL: {comp.CrowdControlScore:F0}";
        _lblDmgSplit.Text = $"DMG: {comp.PhysicalDamagePercent:F0}%P / {comp.MagicDamagePercent:F0}%M";

        // Explanation
        _lblWinExplanation.Text = !string.IsNullOrEmpty(result.OverallAdvice)
            ? result.OverallAdvice
            : "Draft is balanced. BlueStacks CV auto-synchronizes locked picks and bans in real time.";

        // Threats List
        _pnlThreats.SuspendLayout();
        _pnlThreats.Controls.Clear();
        var enemyThreats = result.EnemyComposition?.Strengths ?? [];
        if (enemyThreats.Count > 0)
        {
            foreach (var threat in enemyThreats)
            {
                var lbl = new Label
                {
                    Text = $"⚠ Threat: {threat}",
                    Font = new Font("Segoe UI", 8.2f),
                    ForeColor = Color.FromArgb(251, 191, 36),
                    AutoSize = true,
                    Margin = new Padding(0, 2, 0, 2)
                };
                _pnlThreats.Controls.Add(lbl);
            }
        }
        else
        {
            _pnlThreats.Controls.Add(new Label
            {
                Text = "Waiting for enemy hero picks...",
                Font = new Font("Segoe UI", 8f),
                ForeColor = TextMuted,
                AutoSize = true
            });
        }
        _pnlThreats.ResumeLayout(true);

        // Recommended Heroes Grid
        _pnlRecommendedHeroes.SuspendLayout();
        _pnlRecommendedHeroes.Controls.Clear();
        if (result.RecommendedPicks != null && result.RecommendedPicks.Count > 0)
        {
            foreach (var rec in result.RecommendedPicks.Take(6))
            {
                var heroCard = CreateRecommendedHeroCard(rec);
                _pnlRecommendedHeroes.Controls.Add(heroCard);
            }
        }
        _pnlRecommendedHeroes.ResumeLayout(true);

        // Recommended Items Grid
        _pnlRecommendedItems.SuspendLayout();
        _pnlRecommendedItems.Controls.Clear();
        if (result.ItemCounterSuggestions != null && result.ItemCounterSuggestions.Count > 0)
        {
            foreach (var item in result.ItemCounterSuggestions.Take(6))
            {
                var itemCard = CreateRecommendedItemCard(item);
                _pnlRecommendedItems.Controls.Add(itemCard);
            }
        }
        _pnlRecommendedItems.ResumeLayout(true);
    }

    private Control CreateRecommendedHeroCard(HeroPickRecommendation rec)
    {
        var panel = new Panel
        {
            Size = new Size(130, 68),
            BackColor = Color.FromArgb(24, 34, 53),
            Margin = new Padding(0, 0, 8, 8),
            Cursor = Cursors.Hand
        };

        var pic = new PictureBox
        {
            Size = new Size(44, 44),
            Location = new Point(8, 12),
            Image = ImageCache.GetHeroImage(rec.HeroId, 44, 44),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(pic);

        var lblName = new Label
        {
            Text = rec.Name,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(56, 10),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(lblName);

        var lblScore = new Label
        {
            Text = $"+{rec.Score:F1} Counter",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Location = new Point(56, 28),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(lblScore);

        var lblRole = new Label
        {
            Text = rec.Role ?? "",
            Font = new Font("Segoe UI", 7f),
            ForeColor = TextMuted,
            Location = new Point(56, 44),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(lblRole);

        void OnClick(object? s, EventArgs e)
        {
            var hero = _heroDataService.GetHeroById(rec.HeroId);
            if (hero != null)
            {
                int targetSlot = _draftState.SelectedSide == "ally" ? _draftState.SelectedSlotIndex : _draftState.MyHeroIndex;
                _draftState.SelectHero("ally", targetSlot, hero);
            }
        }

        panel.Click += OnClick;
        pic.Click += OnClick;
        lblName.Click += OnClick;
        lblScore.Click += OnClick;
        lblRole.Click += OnClick;

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }

    private Control CreateRecommendedItemCard(ItemCounterSuggestion item)
    {
        var panel = new Panel
        {
            Size = new Size(130, 68),
            BackColor = Color.FromArgb(24, 34, 53),
            Margin = new Padding(0, 0, 8, 8),
            Cursor = Cursors.Hand
        };

        var pic = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point(8, 14),
            Image = ImageCache.GetItemImage(item.ItemId, 40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(pic);

        var lblName = new Label
        {
            Text = item.ItemName,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = GoldAccent,
            Location = new Point(52, 12),
            Size = new Size(74, 28),
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(lblName);

        var lblReason = new Label
        {
            Text = item.Reason,
            Font = new Font("Segoe UI", 6.8f),
            ForeColor = TextMuted,
            Location = new Point(52, 40),
            Size = new Size(74, 24),
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(lblReason);

        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }

    private void ToggleCvSync()
    {
        _draftState.AutoCvSync = !_draftState.AutoCvSync;
        if (_draftState.AutoCvSync)
        {
            _btnCvSync.Text = "⚡ AUTO-CV SYNC: ON";
            _btnCvSync.BackColor = Color.FromArgb(14, 116, 144);
            _btnCvSync.ForeColor = Color.White;
            _cvSyncTimer.Start();
        }
        else
        {
            _btnCvSync.Text = "⚡ AUTO-CV SYNC: OFF";
            _btnCvSync.BackColor = BgCard;
            _btnCvSync.ForeColor = TextMuted;
            _cvSyncTimer.Stop();
        }
    }

    private void CycleGamePhase()
    {
        string[] phases = ["Draft Pick", "Ban Phase", "Pick Phase", "In-Game Laning", "Standby"];
        int idx = Array.IndexOf(phases, _draftState.CurrentGamePhase);
        int next = (idx + 1) % phases.Length;
        _draftState.SetGamePhase(phases[next]);
    }

    /// <summary>Called from MainForm nav bar phase pill to advance the draft phase.</summary>
    public void TriggerPhaseChange() => CycleGamePhase();

    private void HandleDraftDetectionUpdated(DraftScanResult result)
    {
        if (IsDisposed || !IsHandleCreated) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => HandleDraftDetectionUpdated(result)));
            }
            catch (ObjectDisposedException)
            {
                // Form closing
            }
            return;
        }

        // 1. Sync Detected Game Phase
        if (!string.IsNullOrEmpty(result.DetectedPhase) &&
            !string.Equals(result.DetectedPhase, GamePhase.Standby, StringComparison.OrdinalIgnoreCase))
        {
            if (_draftState.CurrentGamePhase != result.DetectedPhase)
            {
                _draftState.SetGamePhase(result.DetectedPhase);
            }
        }

        // 2. Real-Time Bans Sync (all slots up to MaxBans)
        if (result.Bans != null && result.Bans.Count > 0)
        {
            for (int i = 0; i < result.Bans.Count; i++)
            {
                var ban = result.Bans[i];
                bool isEnemy = i >= 5 || string.Equals(ban.Side, "enemy", StringComparison.OrdinalIgnoreCase);
                int slotIdx = isEnemy ? (i >= 5 ? i - 5 : i) : i;

                if (slotIdx >= _draftState.MaxBans) continue;

                string? heroId = ban.HeroId;
                if (!string.IsNullOrEmpty(heroId) &&
                    !string.Equals(heroId, "Empty", StringComparison.OrdinalIgnoreCase) &&
                    ban.Confidence >= 0.50)
                {
                    var h = _heroDataService.GetHeroById(heroId);
                    if (h != null)
                    {
                        var cur = isEnemy ? _draftState.EnemyBans[slotIdx] : _draftState.AllyBans[slotIdx];
                        if (cur?.Id != h.Id)
                        {
                            _draftState.SelectHero(isEnemy ? "enemy" : "ally", slotIdx, h, isBan: true);
                        }
                    }
                }
            }
        }

        // 3. Real-Time Ally Picks & Lanes & Spells
        if (result.AllyPicks != null)
        {
            for (int i = 0; i < result.AllyPicks.Count && i < 5; i++)
            {
                var pick = result.AllyPicks[i];
                string? heroId = pick.HeroId;
                if (!string.IsNullOrEmpty(heroId) &&
                    !string.Equals(heroId, "Empty", StringComparison.OrdinalIgnoreCase) &&
                    pick.Confidence >= 0.45)
                {
                    var h = _heroDataService.GetHeroById(heroId);
                    if (h != null && _draftState.AllyPicks[i]?.Id != h.Id)
                    {
                        _draftState.SelectHero("ally", i, h);
                    }
                }

                if (!string.IsNullOrEmpty(pick.DetectedLane))
                {
                    string lane = NormalizeLane(pick.DetectedLane);
                    if (_draftState.AllyLanes[i] != lane)
                    {
                        _draftState.SetLane("ally", i, lane);
                    }
                }

                if (!string.IsNullOrEmpty(pick.SpellName) && _draftState.AllySpells[i] != pick.SpellName)
                {
                    _draftState.SetSpell("ally", i, pick.SpellName);
                }
            }
        }

        // 4. Real-Time Enemy Picks & Spells
        if (result.EnemyPicks != null)
        {
            for (int i = 0; i < result.EnemyPicks.Count && i < 5; i++)
            {
                var pick = result.EnemyPicks[i];
                string? heroId = pick.HeroId;
                if (!string.IsNullOrEmpty(heroId) &&
                    !string.Equals(heroId, "Empty", StringComparison.OrdinalIgnoreCase) &&
                    pick.Confidence >= 0.45)
                {
                    var h = _heroDataService.GetHeroById(heroId);
                    if (h != null && _draftState.EnemyPicks[i]?.Id != h.Id)
                    {
                        _draftState.SelectHero("enemy", i, h);
                    }
                }

                if (!string.IsNullOrEmpty(pick.SpellName) && _draftState.EnemySpells[i] != pick.SpellName)
                {
                    _draftState.SetSpell("enemy", i, pick.SpellName);
                }
            }
        }
    }

    private static string NormalizeLane(string raw)
    {
        string l = raw.ToLowerInvariant();
        if (l.Contains("exp")) return "EXP";
        if (l.Contains("gold")) return "Gold";
        if (l.Contains("jungle")) return "Jungle";
        if (l.Contains("roam")) return "Roam";
        if (l.Contains("mid")) return "Mid";
        return "Mid";
    }

    private async Task RunCvSyncCycleAsync()
    {
        if (_isScanning) return;
        _isScanning = true;

        try
        {
            var frame = await _screenCapture.CaptureActiveWindowAsync();
            if (frame == null || !frame.IsValid) return;

            var config = _roiConfig.GetCurrentConfiguration();
            var bannedIds = _draftState.AllyBans.Concat(_draftState.EnemyBans)
                .Where(h => h != null)
                .Select(h => h!.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() => _visionEngine.ProcessCycle(frame, config, bannedIds));
        }
        catch
        {
            // Silently retry next cycle
        }
        finally
        {
            _isScanning = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cvSyncTimer.Stop();
            _cvSyncTimer.Dispose();
            _visionEngine.OnDraftDetectionUpdated -= HandleDraftDetectionUpdated;
            _draftState.OnStateChanged -= SyncFromState;
        }
        base.Dispose(disposing);
    }
}
