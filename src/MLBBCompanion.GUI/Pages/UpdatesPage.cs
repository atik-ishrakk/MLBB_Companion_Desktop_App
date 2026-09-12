namespace MLBBCompanion.GUI.Pages;

public class UpdatesPage : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);
    private static readonly Color BgCard = Color.FromArgb(20, 29, 47);
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    public UpdatesPage()
    {
        Dock = DockStyle.Fill;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        AutoScroll = true;
        Padding = new Padding(24, 20, 24, 40);

        var lblTitle = new Label
        {
            Text = "MLBB COMPANION — SYSTEM SPECIFICATIONS & ARCHITECTURE",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Top,
            Height = 32
        };
        Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = "High-performance .NET 10 desktop application with 3-tier clean architecture and OpenCvSharp4 vision engine.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            Height = 24
        };
        Controls.Add(lblSub);

        // Sections
        AddCard("ARCHITECTURAL LAYOUT (3-TIER CLEAN SEPARATION)",
            "• TIER 1: Presentation & GUI Layer (MLBBCompanion.GUI)\n" +
            "  - Native Windows Pages: DraftPickPage, CheckerPage, EnterpriseHubPage, UpdatesPage\n" +
            "  - Direct high-performance in-memory integration with BLL & DAL services\n\n" +
            "• TIER 2: Business Logic Layer (MLBBCompanion.BLL)\n" +
            "  - Domain entities: Hero, Item, Synergy, GamePhase, DetectionModels, RoiConfiguration\n" +
            "  - Services: DraftRecommenderService, DraftAnalysisService, HeroDataService, PhaseDetectionService, ProcessManager\n\n" +
            "• TIER 3: Data Access & Hardware Layer (MLBBCompanion.DAL)\n" +
            "  - OpenCvSharp4 Vision Engine (Circular-masked ZNCC, CLAHE, 10-slot Hungarian solver)\n" +
            "  - Screen capture: Win32 Desktop & ADB Framebuffer stream\n" +
            "  - Repositories: HeroRepository, RoiRepository, PhaseAnchorStorageService, AdbRepository, ProcessRunner");

        AddCard("COMPUTER VISION ENGINE SPECIFICATIONS",
            "• Template Pre-warming: 136 circular ban templates and 134 rectangular pick templates pre-warmed into unmanaged tensors at startup.\n" +
            "• Circular-Masked ZNCC: 80×80 ban slot matching with zero edge artifacts and circular opacity falloff.\n" +
            "• CLAHE Normalization: Dynamic contrast equalized OpenCV pipeline handles varying emulator graphics brightness.\n" +
            "• 10-Slot Joint Bipartite Solver: Solves global optimal matching across both teams to guarantee zero duplicate heroes.\n" +
            "• Sub-Element Recognizer: Dynamic badge detection for lanes (EXP, Jungle, Mid, Gold, Roam) and battle spells.");

        AddCard("DRAFT STRATEGY & ESPORTS COUNTER FORMULA",
            "• Tactical Counter Scoring: Evaluates active enemy picks and cross-references against hero counters database.\n" +
            "• Wombo-Combo Ally Synergies: Computes synergistic pairs (e.g., Johnson + Odette, Atlas + Claude, Cecilion + Carmilla).\n" +
            "• Dynamic Win-Probability Estimator: Weighted composition algorithm analyzing Durability, CC, Burst, and Scaling.\n" +
            "• Situational Counter Equipment: Recommends items like Antique Cuirass and Dominance Ice against enemy damage archetypes.");

        AddCard("VERSION & CHANGELOG",
            "• v3.0.0 (Native Windows Pages Release):\n" +
            "  - Converted entire browser UI suite from Git repository into native Windows desktop pages.\n" +
            "  - Interactive 5v5 draft pick board with live slot pickers, spell pickers, item builders, and ban controls.\n" +
            "  - Manual CV lab with live 1080p canvas, interactive bounding box overlays, and live anchor cropping.\n" +
            "  - Enterprise telemetry hub with BlueStacks process lifecycle controls and real-time diagnostic console.\n" +
            "  - 100% C# .NET 10 implementation with zero Python dependencies and zero browser extension requirements.");
    }

    private void AddCard(string title, string content)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = BgCard,
            Padding = new Padding(16),
            Margin = new Padding(0, 8, 0, 16)
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Top,
            Height = 24
        };
        card.Controls.Add(lblTitle);

        var lblContent = new Label
        {
            Text = content,
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = TextPrimary,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        card.Controls.Add(lblContent);

        card.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        Controls.Add(card);
        card.BringToFront();
    }
}
