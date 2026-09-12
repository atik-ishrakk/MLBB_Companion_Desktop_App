using System.Drawing.Drawing2D;
using MLBBCompanion.GUI.Services;

namespace MLBBCompanion.GUI.Pages;

public class UpdatesPage : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(10, 15, 29);
    private static readonly Color BgCard = Color.FromArgb(17, 24, 39);
    private static readonly Color BgCardHover = Color.FromArgb(24, 34, 53);
    private static readonly Color BorderColor = Color.FromArgb(40, 53, 76);
    private static readonly Color CyanAccent = Color.FromArgb(6, 182, 212);
    private static readonly Color GreenAccent = Color.FromArgb(52, 211, 153);
    private static readonly Color RedAccent = Color.FromArgb(225, 29, 72);
    private static readonly Color AmberAccent = Color.FromArgb(245, 158, 11);
    private static readonly Color PurpleAccent = Color.FromArgb(168, 85, 247);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextSecondary = Color.FromArgb(203, 213, 225);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    private readonly TextBox _searchBox;
    private readonly FlowLayoutPanel _pnlCards;
    private readonly List<Button> _filterButtons = new();
    private string _activeFilter = "All";

    private sealed record GamePatch(
        string HeroId,
        string HeroName,
        string Role,
        string ChangeType, // "Buff", "Nerf", "Adjustment", "Revamp"
        string PatchVersion,
        string AffectedSkill,
        string Summary,
        string[] Changes
    );

    private readonly List<GamePatch> _allPatches = new()
    {
        new GamePatch("fanny", "Fanny", "Assassin", "Nerf", "Patch 1.9.42",
            "Skill 2 - Steel Cable & Energy Recovery",
            "Slightly increased early-game energy consumption to curtail hyper-aggressive level 2 invades.",
            new[] {
                "• Energy Consumption: 17-12 → 19-14 per consecutive cable cast.",
                "• Passive Energy Regen: Reduced energy restored per prey mark trigger from 10 to 8.",
                "• Base Physical Defense: 19 → 16."
            }),

        new GamePatch("nolan", "Nolan", "Assassin", "Nerf", "Patch 1.9.42",
            "Ultimate - Fracture & Passive Rift Pull",
            "Reduced crowd control vacuum duration and adjusted ultimate escape utility.",
            new[] {
                "• Passive Rift Pull: Pull duration reduced from 0.45s to 0.25s.",
                "• Ultimate Cooldown: 28-20s → 34-26s.",
                "• Cosmic Rift Base Damage: 230-430 + 130% Extra Phys ATK → 200-400 + 120% Extra Phys ATK."
            }),

        new GamePatch("ling", "Ling", "Assassin", "Adjustment", "Patch 1.9.40",
            "Skill 1 - Finch Poise & Skill 2 - Defiant Sword",
            "Enhanced wall traverse agility while lowering raw burst critical strikes on squishy targets.",
            new[] {
                "• Skill 1 Lightness Points: Cost on wall leap decreased from 35 to 30.",
                "• Skill 2 Critical Chance Bonus: 2.5x base crit damage scaling → 2.0x base crit damage scaling.",
                "• Ultimate Sword Gathering: Now grants 1.2s of 20% movement speed upon picking up 4th eye tempest blade."
            }),

        new GamePatch("hayabusa", "Hayabusa", "Assassin", "Nerf", "Patch 1.9.40",
            "Skill 2 - Ninjutsu: Quad Shadow & Ultimate",
            "Tuned early game dive windows to give enemy gold laners more counterplay.",
            new[] {
                "• Skill 2 Cooldown: 18-14s → 20-16s.",
                "• Ougi: Shadow Kill: Base damage per single strike: 140-180 → 120-160.",
                "• Shadow Teleport Energy Cost: 25 → 30 energy."
            }),

        new GamePatch("chou", "Chou", "Fighter", "Buff", "Patch 1.9.42",
            "Skill 1 - Jeet Kune Do & Skill 2 - Shunpo",
            "Enhanced durability and lane wave clearing to reinforce EXP lane viability against meta sustain fighters.",
            new[] {
                "• Skill 1 Base Damage: 180-330 + 70% Total Phys ATK → 210-360 + 80% Total Phys ATK.",
                "• Skill 2 Physical Penetration: 15-40 → 20-50 physical penetration during shield.",
                "• Base HP Growth: 202 → 220 per level."
            }),

        new GamePatch("paquito", "Paquito", "Fighter", "Buff", "Patch 1.9.40",
            "Skill 2 - Jab & Champ Stance",
            "Improved poke rotation burst and passive stack accumulation in jungle rotations.",
            new[] {
                "• Skill 2 Jab Base Damage: 240-440 + 100% Total Phys ATK → 260-480 + 110% Total Phys ATK.",
                "• Enhanced Jab Shield: 150-500 + 110% Total Phys ATK → 180-550 + 120% Total Phys ATK.",
                "• Passive Champ Stance: Stack retention timer out of combat: 4s → 6s."
            }),

        new GamePatch("harith", "Harith", "Mage", "Nerf", "Patch 1.9.42",
            "Skill 2 - Chrono Dash & Ultimate - Zaman Force",
            "Reduced shield frequency and burst scaling in the Gold Lane.",
            new[] {
                "• Skill 2 Shield: 150-300 + 120% Magic Power → 120-260 + 100% Magic Power.",
                "• Skill 2 Enhanced Basic Attack: 200-350 + 80% Magic Power → 180-320 + 70% Magic Power.",
                "• Ultimate Zaman Force Slow: 35% → 25% initial slow inside the cross path."
            }),

        new GamePatch("cici", "Cici", "Fighter", "Buff", "Patch 1.9.42",
            "Skill 1 - Yo-Yo Blitz & Passive - Performer's Delight",
            "Increased mobility and single-target lockdown damage against high-durability frontline tanks.",
            new[] {
                "• Skill 1 Max HP% Damage: 3.5%-5.5% Max HP → 4.0%-6.5% Max HP per hit.",
                "• Movement Speed Bonus at Full Stacks: 15% → 20%.",
                "• Base Physical Lifesteal bonus per Delight stack: 0.8% → 1.2%."
            }),

        new GamePatch("suyou", "Suyou", "Fighter", "Revamp", "Patch 1.9.38",
            "Dual Aspect Mastery (Mortal Form & Immortal Form)",
            "Complete tuning of dual stance transitions, granting distinctive burst in Mortal and sustain in Immortal.",
            new[] {
                "• Mortal Stance: Enhanced tap cast delivers 25% higher raw physical burst with armor shred.",
                "• Immortal Stance: Hold cast grants 30%-45% damage reduction and crowd control immunity.",
                "• Skill 1 Dash Hitbox: Widened by 15% to improve corner wall transitions."
            }),

        new GamePatch("zhuxin", "Zhuxin", "Mage", "Nerf", "Patch 1.9.40",
            "Skill 2 - Fluttering Grace & Mana Consumption",
            "Adjusted airborne disruption frequency to balance teamfight control.",
            new[] {
                "• Skill 2 Mana per Second: 40-70 → 55-90 mana drain during continuous channeling.",
                "• Airborne Toss Delay: Target stack requirement increased from 8 to 10 Crimson Butterfly stacks.",
                "• Ultimate Shield: 400-800 + 100% Magic Power → 320-680 + 80% Magic Power."
            }),

        new GamePatch("beatrix", "Beatrix", "Marksman", "Buff", "Patch 1.9.40",
            "Renner (Sniper) & Wesker (Shotgun)",
            "Enhanced long-range precision bullet speed and tightened close-range pellet spread.",
            new[] {
                "• Renner Bullet Velocity: Increased by 18%, reducing lead-time required on moving targets.",
                "• Renner Base Damage: 350-650 + 180% Phys ATK → 380-700 + 190% Phys ATK.",
                "• Wesker Pellet Damage Decay on multiple hits: 40% → 45% minimum decay."
            }),

        new GamePatch("claude", "Claude", "Marksman", "Adjustment", "Patch 1.9.40",
            "Skill 1 - Art of Thievery & Ultimate - Blazing Duet",
            "Streamlined stack maintenance in exchange for minor base attack speed adjustments.",
            new[] {
                "• Skill 1 Stack Duration: 6s → 8s (easier to maintain 10 stacks between minion waves).",
                "• Skill 1 Attack Speed Stolen: 2.5%-5% per stack → 2%-4% per stack.",
                "• Ultimate Blazing Duet: Base shield on cast: 200-400 → 250-450."
            }),

        new GamePatch("fredrinn", "Fredrinn", "Tank", "Nerf", "Patch 1.9.38",
            "Passive - Crystalline Armor & Ultimate - Appraiser's Wrath",
            "Lowered the gray health conversion ratio in teamfights to curtail absurd raid-boss survivability.",
            new[] {
                "• Gray Health Conversion: 85% damage stored → 75% damage stored as gray health.",
                "• Gray Health into HP Conversion: 30% (+100% Total Phys ATK) → 25% (+80% Total Phys ATK).",
                "• Appraiser's Wrath Base Damage: 600-1000 → 500-900."
            }),

        new GamePatch("tigreal", "Tigreal", "Tank", "Adjustment", "Patch 1.9.40",
            "Skill 2 - Sacred Hammer & Ultimate - Implosion",
            "Improved hammer charge collision reliability while smoothing the startup frames of Implosion.",
            new[] {
                "• Skill 2 Second Cast Knockup: Hitbox width increased by 20%.",
                "• Ultimate Implosion: Startup channel time before pull: 1.4s → 1.3s.",
                "• Ultimate Cooldown: 45-37s → 48-40s."
            }),

        new GamePatch("mathilda", "Mathilda", "Support", "Nerf", "Patch 1.9.38",
            "Skill 2 - Guiding Wind & Ultimate - Circling Eagle",
            "Restrained the teamwide mobility shield and target-lock stun duration.",
            new[] {
                "• Skill 2 Guiding Wind Shield: 500-800 + 180% Magic Power → 400-700 + 140% Magic Power.",
                "• Ally Shield Share: 70% of Mathilda's shield → 55% of shield.",
                "• Ultimate Stun Duration: 0.8s → 0.6s upon collision."
            }),

        new GamePatch("valentina", "Valentina", "Mage", "Adjustment", "Patch 1.9.40",
            "Ultimate - I Am You & Passive - Primal Curse",
            "Refined cooldown calculation when stealing high-impact teamfight ultimates.",
            new[] {
                "• Stolen Ultimate Cooldown: Now set to 120% of the target's natural cooldown (minimum 40s).",
                "• Passive EXP Generation: 8-32 EXP per hit → 10-36 EXP per hit when lower level than target.",
                "• Skill 1 Shadow Strike Base Damage: 340-640 → 360-680."
            }),

        new GamePatch("julian", "Julian", "Mage", "Buff", "Patch 1.9.42",
            "Enhanced Skill 1 - Scythe & Enhanced Skill 3 - Chain",
            "Boosted late-game AP scaling and reduced cooldown on enhanced rotation finishers.",
            new[] {
                "• Enhanced Skill 1 Scythe: 65-105 + 50% Magic Power → 80-120 + 60% Magic Power per hit.",
                "• Passive Enhanced Cooldown: 10-7s → 9-6s.",
                "• Base Magic Lifesteal from Passive: 15% + 1.5% per level → 18% + 2% per level."
            }),

        new GamePatch("wanwan", "Wanwan", "Marksman", "Buff", "Patch 1.9.42",
            "Passive - Tiger's Pace & Ultimate - Crossbow of Tang",
            "Improved jump responsiveness and reduced the attack lock vulnerability on minion waves.",
            new[] {
                "• Jump Dash Distance: Slightly scaled with attack speed (+10% velocity at 2.0+ AS).",
                "• Weakness Break Detection: Hit angle tolerance widened by 12 degrees for side weaknesses.",
                "• Ultimate Crossbow of Tang: Base physical scaling per arrow: 60-72 + 35% Total Phys ATK → 65-78 + 38% Total Phys ATK."
            }),

        new GamePatch("granger", "Granger", "Marksman", "Revamp", "Patch 1.9.36",
            "Energy System & Death Sonata Overhaul",
            "Updated the six-bullet reloading mechanism and eliminated energy constraints on Skill 2.",
            new[] {
                "• Skill 1 Rhapsody: Now pierces through 1 minor minion target with 50% decay.",
                "• Skill 2 Rondo Dash: Cooldown resets on 6th bullet critical hit.",
                "• Ultimate Death Sonata: Projectile speed increased by 25%; splash blast radius widened."
            }),

        new GamePatch("novaria", "Novaria", "Mage", "Adjustment", "Patch 1.9.40",
            "Skill 2 - Astral Recall & Ultimate - Astral Echo",
            "Broadened sphere snipe collision profile at max distance while trimming ultimate reveal area.",
            new[] {
                "• Skill 2 Sphere Hitbox at 200% Range: Radius increased from 1.0 to 1.25 yards.",
                "• Skill 2 Max Damage: 300-600 + 150% Magic Power + 4%-8% Enemy Max HP → 280-560 + 140% Magic Power + 5%-9% Enemy Max HP.",
                "• Ultimate Astral Echo Cone Width: 60 degrees → 50 degrees."
            })
    };

    public UpdatesPage()
    {
        Dock = DockStyle.Fill;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        Padding = new Padding(20, 16, 20, 20);

        // Header Panel
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
        Controls.Add(pnlHeader);

        var lblTitle = new Label
        {
            Text = "MLBB GAME BALANCE UPDATES & PATCH NOTES",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(0, 4)
        };
        pnlHeader.Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = "Official hero adjustments, buffs, nerfs, balance tuning, and revamps for competitive draft play.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(0, 30)
        };
        pnlHeader.Controls.Add(lblSub);

        // Control & Filter Bar (Search Box + Filter Chips)
        var pnlControls = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 12)
        };
        Controls.Add(pnlControls);

        _searchBox = new TextBox
        {
            PlaceholderText = "🔍 Search hero name, skill or keyword...",
            BackColor = BgCard,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(270, 30),
            Location = new Point(0, 6)
        };
        _searchBox.TextChanged += (_, _) => PopulateCards();
        pnlControls.AddControlFixed(_searchBox);

        var filterFlow = new FlowLayoutPanel
        {
            Location = new Point(285, 4),
            Size = new Size(Width - 300, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        pnlControls.Controls.Add(filterFlow);

        string[] filters = ["All", "Buff", "Nerf", "Adjustment", "Revamp"];
        foreach (var f in filters)
        {
            var btn = new Button
            {
                Text = f.ToUpperInvariant(),
                Tag = f,
                BackColor = f == "All" ? CyanAccent : BgCard,
                ForeColor = f == "All" ? Color.Black : TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                Height = 30,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = f == "All" ? CyanAccent : BorderColor;

            btn.Click += (_, _) =>
            {
                _activeFilter = f;
                foreach (var b in _filterButtons)
                {
                    bool isActive = (string)b.Tag! == _activeFilter;
                    b.BackColor = isActive ? GetFilterColor(_activeFilter) : BgCard;
                    b.ForeColor = isActive ? Color.Black : TextSecondary;
                    b.FlatAppearance.BorderColor = isActive ? GetFilterColor(_activeFilter) : BorderColor;
                }
                PopulateCards();
            };

            _filterButtons.Add(btn);
            filterFlow.Controls.Add(btn);
        }

        // Cards Scrollable Container
        _pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 6, 12, 16)
        };
        Controls.Add(_pnlCards);
        _pnlCards.BringToFront();

        PopulateCards();
    }

    private Color GetFilterColor(string filter) => filter switch
    {
        "Buff" => GreenAccent,
        "Nerf" => RedAccent,
        "Adjustment" => CyanAccent,
        "Revamp" => PurpleAccent,
        _ => CyanAccent
    };

    private void PopulateCards()
    {
        _pnlCards.SuspendLayout();
        _pnlCards.Controls.Clear();

        var query = _searchBox.Text.Trim().ToLowerInvariant();
        var filtered = _allPatches.Where(p =>
        {
            if (_activeFilter != "All" && !string.Equals(p.ChangeType, _activeFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(query))
            {
                return p.HeroName.ToLowerInvariant().Contains(query) ||
                       p.HeroId.ToLowerInvariant().Contains(query) ||
                       p.Role.ToLowerInvariant().Contains(query) ||
                       p.AffectedSkill.ToLowerInvariant().Contains(query) ||
                       p.Summary.ToLowerInvariant().Contains(query) ||
                       p.Changes.Any(c => c.ToLowerInvariant().Contains(query));
            }
            return true;
        }).ToList();

        if (filtered.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "No patch updates match the current filter or search criteria.",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextMuted,
                AutoSize = true,
                Padding = new Padding(12, 24, 0, 0)
            };
            _pnlCards.Controls.Add(lblEmpty);
        }
        else
        {
            int cardWidth = Math.Max(720, _pnlCards.Width - 30);
            foreach (var patch in filtered)
            {
                _pnlCards.Controls.Add(CreatePatchCard(patch, cardWidth));
            }
        }

        _pnlCards.ResumeLayout(true);
    }

    private Control CreatePatchCard(GamePatch patch, int width)
    {
        var card = new Panel
        {
            Width = width,
            AutoSize = true,
            BackColor = BgCard,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Default
        };

        // Top Header of Card
        var pnlHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        card.Controls.Add(pnlHeader);

        // Hero Avatar
        var imgAvatar = ImageCache.GetHeroImage(patch.HeroId, 38, 38, circular: true);
        var picAvatar = new PictureBox
        {
            Size = new Size(38, 38),
            Image = imgAvatar,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 10, 0)
        };
        pnlHeader.Controls.Add(picAvatar);

        // Hero Name & Role
        var pnlName = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 12, 0)
        };
        pnlHeader.Controls.Add(pnlName);

        var lblHero = new Label
        {
            Text = patch.HeroName.ToUpperInvariant(),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Margin = new Padding(0)
        };
        pnlName.Controls.Add(lblHero);

        var lblRole = new Label
        {
            Text = $"{patch.Role.ToUpperInvariant()} • {patch.PatchVersion}",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = TextMuted,
            AutoSize = true,
            Margin = new Padding(0)
        };
        pnlName.Controls.Add(lblRole);

        // Change Badge: [▲ BUFF], [▼ NERF], [◆ ADJUSTMENT], [★ REVAMP]
        var badgeColor = patch.ChangeType switch
        {
            "Buff" => GreenAccent,
            "Nerf" => RedAccent,
            "Adjustment" => CyanAccent,
            "Revamp" => PurpleAccent,
            _ => AmberAccent
        };

        var badgeText = patch.ChangeType switch
        {
            "Buff" => "▲ BUFF",
            "Nerf" => "▼ NERF",
            "Adjustment" => "◆ ADJUSTMENT",
            "Revamp" => "★ REVAMP",
            _ => patch.ChangeType.ToUpperInvariant()
        };

        var lblBadge = new Label
        {
            Text = badgeText,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = badgeColor,
            BackColor = Color.FromArgb(30, badgeColor.R, badgeColor.G, badgeColor.B),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(4, 4, 0, 0)
        };
        lblBadge.Paint += (_, e) =>
        {
            using var pen = new Pen(badgeColor, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, lblBadge.Width - 1, lblBadge.Height - 1);
        };
        pnlHeader.Controls.Add(lblBadge);

        // Affected Skill / Subsystem Label
        var lblSkill = new Label
        {
            Text = $"Target: {patch.AffectedSkill}",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = CyanAccent,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4)
        };
        card.Controls.Add(lblSkill);
        lblSkill.BringToFront();

        // Summary Text
        var lblSummary = new Label
        {
            Text = patch.Summary,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Italic),
            ForeColor = TextSecondary,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 2, 0, 6)
        };
        card.Controls.Add(lblSummary);
        lblSummary.BringToFront();

        // Bulleted Details Panel
        var pnlDetails = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(12, 18, 30),
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 4, 0, 0)
        };
        card.Controls.Add(pnlDetails);
        pnlDetails.BringToFront();

        foreach (var change in patch.Changes)
        {
            var lblChange = new Label
            {
                Text = change,
                Font = new Font("Consolas", 8.8f),
                ForeColor = change.Contains("→") ? Color.FromArgb(240, 245, 255) : TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };
            pnlDetails.Controls.Add(lblChange);
        }

        // Card Border Drawing
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        return card;
    }
}

internal static class ControlExtensions
{
    public static void AddControlFixed(this Panel panel, Control control)
    {
        panel.Controls.Add(control);
    }
}
