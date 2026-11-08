using BepInEx.Configuration;
using UnityEngine;

namespace Submarines
{
    /// <summary>All tunables. Written to BepInEx/config/com.badnew21.submarines.cfg on first run.</summary>
    internal static class SubConfig
    {
        // Hulls
        internal static ConfigEntry<string> AttackDonorKeys;
        internal static ConfigEntry<string> BallisticDonorKeys;
        internal static ConfigEntry<float> AttackCost;
        internal static ConfigEntry<float> BallisticCost;

        // Depth
        internal static ConfigEntry<float> PeriscopeDepth;
        internal static ConfigEntry<float> PatrolDepth;
        internal static ConfigEntry<float> MaxDepth;
        internal static ConfigEntry<float> LaunchDepth;
        internal static ConfigEntry<float> BottomClearance;
        internal static ConfigEntry<float> VerticalSpeed;

        // Detection
        internal static ConfigEntry<float> SubmergedThreshold;
        internal static ConfigEntry<float> MastRadarRange;
        internal static ConfigEntry<float> MastVisualRange;
        internal static ConfigEntry<bool> SurfaceShipsGetSonar;
        internal static ConfigEntry<float> ThermalLayerDepth;
        internal static ConfigEntry<float> ThermalLayerLossDb;

        // Weapons
        internal static ConfigEntry<int> AttackTorpedoes;
        internal static ConfigEntry<float> TorpedoReload;
        internal static ConfigEntry<float> TorpedoRange;
        internal static ConfigEntry<float> TorpedoSpeed;
        internal static ConfigEntry<float> TorpedoDamage;
        internal static ConfigEntry<int> BallisticMissiles;
        internal static ConfigEntry<float> BallisticInterval;
        internal static ConfigEntry<float> BallisticRange;
        internal static ConfigEntry<string> BallisticMissileKeys;
        internal static ConfigEntry<bool> BallisticAutoLaunch;
        internal static ConfigEntry<bool> SurfaceAsw;
        internal static ConfigEntry<int> SurfaceAswTorpedoes;
        internal static ConfigEntry<float> SurfaceAswRange;
        internal static ConfigEntry<bool> StockWarheadVisuals;
        internal static ConfigEntry<string> WarheadVisualMissileKeys;

        // Spoofing
        internal static ConfigEntry<int> Decoys;
        internal static ConfigEntry<float> DecoyLifetime;
        internal static ConfigEntry<float> DecoySourceLevel;
        internal static ConfigEntry<float> DecoyCooldown;
        internal static ConfigEntry<float> FalseEchoOffset;
        internal static ConfigEntry<float> MimicDuration;
        internal static ConfigEntry<float> MimicCooldown;

        // UI
        internal static ConfigEntry<KeyCode> DisplayKey;
        internal static ConfigEntry<KeyCode> NextSubKey;
        internal static ConfigEntry<KeyCode> DeeperKey;
        internal static ConfigEntry<KeyCode> ShallowerKey;
        internal static ConfigEntry<KeyCode> TorpedoKey;
        internal static ConfigEntry<KeyCode> DecoyKey;
        internal static ConfigEntry<KeyCode> MimicKey;
        internal static ConfigEntry<KeyCode> LaunchKey;
        internal static ConfigEntry<KeyCode> PingKey;
        internal static ConfigEntry<bool> ShowTruth;
        internal static ConfigEntry<bool> VerboseLog;

        internal static void Bind(ConfigFile cfg)
        {
            AttackDonorKeys = cfg.Bind("Hulls", "AttackSubDonor", "corvette,frigate,destroyer",
                "Comma-separated keywords (matched against stock ship jsonKey/unitName, first hit wins) for the hull the SSN is cloned from. The game log lists every ship on startup.");
            BallisticDonorKeys = cfg.Bind("Hulls", "BallisticSubDonor", "destroyer,cruiser,frigate",
                "Keywords for the hull the SSBN is cloned from.");
            AttackCost = cfg.Bind("Hulls", "AttackSubCost", 2400f, "Encyclopedia value of the SSN (millions).");
            BallisticCost = cfg.Bind("Hulls", "BallisticSubCost", 4800f, "Encyclopedia value of the SSBN (millions).");

            PeriscopeDepth = cfg.Bind("Depth", "PeriscopeDepth", 14f, "Metres below sea level. Masts are exposed at this depth.");
            PatrolDepth = cfg.Bind("Depth", "PatrolDepth", 120f, "Default cruising depth (m).");
            MaxDepth = cfg.Bind("Depth", "MaxDepth", 300f, "Deepest commanded depth (m).");
            LaunchDepth = cfg.Bind("Depth", "LaunchDepth", 30f, "Depth the SSBN climbs to before launching.");
            BottomClearance = cfg.Bind("Depth", "BottomClearance", 25f, "Keep at least this far above the seabed.");
            VerticalSpeed = cfg.Bind("Depth", "VerticalSpeed", 4f, "Max dive/climb rate (m/s).");

            SubmergedThreshold = cfg.Bind("Detection", "SubmergedThreshold", 8f,
                "Hull origin deeper than this (m) counts as submerged: invisible to surface radar.");
            MastRadarRange = cfg.Bind("Detection", "MastRadarRange", 9000f,
                "At periscope depth, radars within this range (m) can still paint the masts. 0 = never.");
            MastVisualRange = cfg.Bind("Detection", "MastVisualRange", 2500f,
                "At periscope depth, optical sensors within this range (m) can see the masts.");
            SurfaceShipsGetSonar = cfg.Bind("Detection", "SurfaceShipsGetSonar", true,
                "Give stock warships a hull sonar so they can hunt submarines.");
            ThermalLayerDepth = cfg.Bind("Detection", "ThermalLayerDepth", 60f, "Depth of the thermocline (m).");
            ThermalLayerLossDb = cfg.Bind("Detection", "ThermalLayerLossDb", 14f,
                "Extra transmission loss when source and receiver are on opposite sides of the layer.");

            AttackTorpedoes = cfg.Bind("Weapons", "AttackSubTorpedoes", 18, "Torpedo load for the SSN.");
            TorpedoReload = cfg.Bind("Weapons", "TorpedoReload", 25f, "Seconds between torpedo shots per boat.");
            TorpedoRange = cfg.Bind("Weapons", "TorpedoRange", 22000f, "Torpedo run (m).");
            TorpedoSpeed = cfg.Bind("Weapons", "TorpedoSpeed", 26f, "Torpedo speed (m/s). 26 m/s is roughly 50 knots.");
            TorpedoDamage = cfg.Bind("Weapons", "TorpedoDamage", 300f, "Warhead (kg TNT equivalent).");
            BallisticMissiles = cfg.Bind("Weapons", "BallisticSubMissiles", 12, "SLBM tubes on the SSBN.");
            BallisticInterval = cfg.Bind("Weapons", "BallisticInterval", 45f, "Seconds between SLBM launches.");
            BallisticRange = cfg.Bind("Weapons", "BallisticRange", 150000f, "Max target distance for an SLBM (m).");
            BallisticMissileKeys = cfg.Bind("Weapons", "BallisticMissileKeys", "ballistic,tbm,srbm",
                "Keywords used to pick the stock missile the SSBN fires. The log lists every missile on startup.");
            BallisticAutoLaunch = cfg.Bind("Weapons", "BallisticAutoLaunch", true,
                "AI SSBNs launch at targets their faction knows about. Off = only on player command.");
            SurfaceAsw = cfg.Bind("Weapons", "SurfaceShipAsw", true,
                "Warships with sonar launch ASW torpedoes at submarines they hold a firm contact on.");
            SurfaceAswTorpedoes = cfg.Bind("Weapons", "SurfaceShipAswTorpedoes", 6, "ASW torpedoes per surface warship.");
            SurfaceAswRange = cfg.Bind("Weapons", "SurfaceShipAswRange", 9000f, "Max ASW engagement range (m).");
            StockWarheadVisuals = cfg.Bind("Weapons", "StockWarheadVisuals", true,
                "Spawn and detonate a stock missile at torpedo impact so everyone sees the explosion.");
            WarheadVisualMissileKeys = cfg.Bind("Weapons", "WarheadVisualMissileKeys", "antiship,ashm,cruise,ballistic",
                "Keywords for the stock missile used for impact visuals.");

            Decoys = cfg.Bind("Spoofing", "DecoysPerBoat", 8, "Noisemaker / repeater decoys carried by each sub.");
            DecoyLifetime = cfg.Bind("Spoofing", "DecoyLifetime", 75f, "Seconds a decoy keeps transmitting.");
            DecoySourceLevel = cfg.Bind("Spoofing", "DecoySourceLevel", 138f, "Decoy loudness (dB). Subs are ~105-115.");
            DecoyCooldown = cfg.Bind("Spoofing", "DecoyCooldown", 20f, "Seconds between decoy launches.");
            FalseEchoOffset = cfg.Bind("Spoofing", "FalseEchoOffset", 1800f,
                "How far (m) a repeater decoy walks its false echo away from the real boat.");
            MimicDuration = cfg.Bind("Spoofing", "MimicDuration", 90f,
                "Seconds the acoustic mimicry (sound like a merchant/biologic) lasts.");
            MimicCooldown = cfg.Bind("Spoofing", "MimicCooldown", 180f, "Cooldown for mimicry.");

            DisplayKey = cfg.Bind("UI", "SonarDisplayKey", KeyCode.F8, "Toggle the sonar display.");
            NextSubKey = cfg.Bind("UI", "NextSubKey", KeyCode.F9, "Cycle which friendly sub the display follows.");
            DeeperKey = cfg.Bind("UI", "DeeperKey", KeyCode.PageDown, "Order selected sub deeper.");
            ShallowerKey = cfg.Bind("UI", "ShallowerKey", KeyCode.PageUp, "Order selected sub shallower.");
            TorpedoKey = cfg.Bind("UI", "TorpedoKey", KeyCode.Insert, "Fire a torpedo at the strongest hostile contact.");
            DecoyKey = cfg.Bind("UI", "DecoyKey", KeyCode.Delete, "Launch a decoy.");
            MimicKey = cfg.Bind("UI", "MimicKey", KeyCode.End, "Engage acoustic mimicry.");
            LaunchKey = cfg.Bind("UI", "LaunchKey", KeyCode.Home, "SSBN: launch at the nearest known enemy target.");
            PingKey = cfg.Bind("UI", "PingKey", KeyCode.Pause, "Send an active sonar ping (reveals you).");
            ShowTruth = cfg.Bind("UI", "ShowTruth", false, "Debug: mark false contacts on the display.");
            VerboseLog = cfg.Bind("UI", "VerboseLog", false, "Log sonar and weapon events.");
        }

        internal static string[] Keys(ConfigEntry<string> e)
        {
            string raw = e != null && e.Value != null ? e.Value : string.Empty;
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim().ToLowerInvariant();
            return parts;
        }
    }
}
