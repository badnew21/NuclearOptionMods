using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Runtime
{
    internal enum WarheadMode : byte
    {
        /// <summary>Always fire the conventional SLBM.</summary>
        Conventional,

        /// <summary>Fire nuclear when release is authorised and warheads are available, otherwise conventional.</summary>
        Nuclear,

        /// <summary>Fire nuclear or hold fire.</summary>
        NuclearOnly,
    }

    internal enum ReleaseBlock : byte
    {
        None,
        NoNuclearMissile,
        Escalation,
        NoWarheads,
        NoMissile,
    }

    /// <summary>
    /// Picks the SLBM to fire and applies the game's nuclear rules: the mission's escalation level
    /// (tactical/strategic) and the faction warhead stockpile, which a nuclear launch draws from.
    /// The rules are the same ones the stock game and the PlayableVehicles mod use.
    /// </summary>
    internal static class Nukes
    {
        private static bool _resolved;
        private static MissileDefinition _nuclear;
        private static WeaponInfo _nuclearInfo;
        private static MissileDefinition _conventional;

        internal static MissileDefinition NuclearMissile { get { Resolve(); return _nuclear; } }
        internal static MissileDefinition ConventionalMissile { get { Resolve(); return _conventional; } }
        internal static bool NuclearIsStrategic { get { Resolve(); return _nuclearInfo != null && _nuclearInfo.strategic; } }

        internal static void Reset()
        {
            _resolved = false;
        }

        /// <summary>
        /// Sort the stock ballistic missiles into nuclear and conventional. A missile counts as
        /// ballistic if its name matches BallisticMissileKeys, or its weapon info says it is an
        /// over-the-horizon missile. Keyword matches win ties.
        /// </summary>
        private static void Resolve()
        {
            if (_resolved)
                return;
            Encyclopedia enc = Encyclopedia.i;
            if (enc == null || enc.missiles == null)
                return;
            _resolved = true;
            _nuclear = null;
            _nuclearInfo = null;
            _conventional = null;

            string[] keys = SubConfig.Keys(SubConfig.BallisticMissileKeys);
            int bestNuc = -1, bestConv = -1;
            foreach (MissileDefinition m in enc.missiles)
            {
                if (m == null || m.unitPrefab == null)
                    continue;
                WeaponInfo info = InfoFor(enc, m);
                string hay = (m.jsonKey + " " + m.unitName).ToLowerInvariant();
                int keyRank = -1;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (keys[k].Length > 0 && hay.Contains(keys[k]))
                    {
                        keyRank = keys.Length - k;
                        break;
                    }
                }
                bool ballistic = keyRank >= 0 || (info != null && info.missile && info.overHorizon);
                if (!ballistic)
                    continue;
                int score = keyRank >= 0 ? 100 + keyRank : 0;
                bool nuclear = info != null && info.nuclear;
                if (nuclear && score > bestNuc)
                {
                    bestNuc = score;
                    _nuclear = m;
                    _nuclearInfo = info;
                }
                else if (!nuclear && score > bestConv)
                {
                    bestConv = score;
                    _conventional = m;
                }
            }

            SubmarinesPlugin.Log.LogInfo("SLBM loadout: nuclear=" + Describe(_nuclear) +
                                         (_nuclearInfo != null ? (_nuclearInfo.strategic ? " (strategic)" : " (tactical)") : "") +
                                         ", conventional=" + Describe(_conventional));
        }

        private static string Describe(MissileDefinition m)
        {
            return m != null ? m.unitName + " [" + m.jsonKey + "]" : "none";
        }

        /// <summary>The WeaponInfo that launches this missile: from a weapon mount, else from the prefab.</summary>
        private static WeaponInfo InfoFor(Encyclopedia enc, MissileDefinition m)
        {
            if (enc.weaponMounts != null)
            {
                foreach (WeaponMount mount in enc.weaponMounts)
                {
                    if (mount != null && mount.info != null && mount.info.weaponPrefab == m.unitPrefab)
                        return mount.info;
                }
            }
            Missile prefabMissile = m.unitPrefab.GetComponent<Missile>();
            if (prefabMissile != null)
            {
                try { return prefabMissile.GetWeaponInfo(); }
                catch { }
            }
            return null;
        }

        /// <summary>Is nuclear release allowed for this faction right now?</summary>
        internal static ReleaseBlock CanRelease(FactionHQ hq)
        {
            if (NuclearMissile == null)
                return ReleaseBlock.NoNuclearMissile;
            if (!SubConfig.RespectNuclearRules.Value)
                return ReleaseBlock.None;
            bool escalation;
            try { escalation = NuclearIsStrategic ? MissionManager.AllowStrategic() : MissionManager.AllowTactical(); }
            catch { escalation = false; }
            if (!escalation)
                return ReleaseBlock.Escalation;
            if (Stockpile(hq) < 1)
                return ReleaseBlock.NoWarheads;
            return ReleaseBlock.None;
        }

        internal static int Stockpile(FactionHQ hq)
        {
            if (hq == null)
                return 0;
            try { return hq.GetWarheadStockpile(); }
            catch { return 0; }
        }

        /// <summary>Which missile this boat fires, given its warhead setting. Null = hold fire.</summary>
        internal static MissileDefinition Choose(WarheadMode mode, FactionHQ hq, out bool nuclear, out ReleaseBlock block)
        {
            nuclear = false;
            block = ReleaseBlock.None;
            if (mode != WarheadMode.Conventional)
            {
                block = CanRelease(hq);
                if (block == ReleaseBlock.None)
                {
                    nuclear = true;
                    return NuclearMissile;
                }
                if (mode == WarheadMode.NuclearOnly)
                    return null;
            }
            if (ConventionalMissile == null)
            {
                block = ReleaseBlock.NoMissile;
                return null;
            }
            return ConventionalMissile;
        }

        /// <summary>Take one warhead out of the faction's airbase storage, like a stock nuclear loadout does.</summary>
        internal static void ConsumeWarhead(FactionHQ hq)
        {
            if (hq == null || !SubConfig.RespectNuclearRules.Value)
                return;
            int needed = 1;
            try
            {
                foreach (Airbase airbase in FactionRegistry.airbaseLookup.Values)
                {
                    if (needed <= 0)
                        break;
                    if (airbase == null || airbase.CurrentHQ != hq || !airbase.HasStorage())
                        continue;
                    int take = Mathf.Min(needed, airbase.GetWarheads());
                    if (take <= 0)
                        continue;
                    airbase.RemoveWarheads(take);
                    needed -= take;
                }
            }
            catch (System.Exception ex)
            {
                SubmarinesPlugin.Log.LogWarning("Could not draw a warhead from the stockpile: " + ex.Message);
            }
            if (needed > 0)
                SubmarinesPlugin.Log.LogWarning("Nuclear SLBM fired but no airbase had a warhead to remove.");
        }

        internal static string Label(WarheadMode m)
        {
            switch (m)
            {
                case WarheadMode.Nuclear: return "NUC";
                case WarheadMode.NuclearOnly: return "NUC ONLY";
                default: return "CONV";
            }
        }

        internal static string BlockText(ReleaseBlock b)
        {
            switch (b)
            {
                case ReleaseBlock.NoNuclearMissile: return "no nuclear SLBM in this game";
                case ReleaseBlock.Escalation: return NuclearIsStrategic ? "strategic release not authorised" : "nuclear release not authorised";
                case ReleaseBlock.NoWarheads: return "no warheads in the stockpile";
                case ReleaseBlock.NoMissile: return "no conventional SLBM found";
                default: return string.Empty;
            }
        }

        internal static WarheadMode Parse(string s)
        {
            string v = (s ?? string.Empty).Trim().ToLowerInvariant();
            if (v == "conventional" || v == "conv")
                return WarheadMode.Conventional;
            if (v == "nuclearonly" || v == "nuclear only")
                return WarheadMode.NuclearOnly;
            return WarheadMode.Nuclear;
        }
    }
}
