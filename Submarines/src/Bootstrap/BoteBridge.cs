using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Submarines.Bootstrap
{
    /// <summary>
    /// Optional integration with Boscali Ocean Training Exercise (BOTE, com.minec.bote), which makes
    /// ships player-drivable by turning them into special aircraft that spawn from docks and
    /// carriers. When BOTE is installed, this registers a player version of each submarine:
    /// a clone of one of BOTE's own player ships with the submarine model and systems, offered by
    /// every hangar that offers the donor ship.
    ///
    /// Everything goes through reflection, so the mod still loads and works without BOTE.
    /// </summary>
    internal static class BoteBridge
    {
        internal const string BoteGuid = "com.minec.bote";

        private static Type _modAssetsType;
        private static ICollection<AircraftDefinition> _shipDefs;
        private static bool _registered;

        /// <summary>Player sub -> the BOTE ship it was built from. A hangar offers the sub wherever it offers the donor.</summary>
        private static readonly Dictionary<AircraftDefinition, AircraftDefinition> DonorOf = new Dictionary<AircraftDefinition, AircraftDefinition>();

        internal static AircraftDefinition Donor(AircraftDefinition sub)
        {
            AircraftDefinition d;
            return sub != null && DonorOf.TryGetValue(sub, out d) ? d : null;
        }

        /// <summary>Player subs whose donor this list contains (to append to a hangar's offer).</summary>
        internal static List<AircraftDefinition> SubsOfferedWith(AircraftDefinition[] list)
        {
            List<AircraftDefinition> extra = null;
            if (list == null || DonorOf.Count == 0)
                return null;
            foreach (KeyValuePair<AircraftDefinition, AircraftDefinition> kv in DonorOf)
            {
                if (Array.IndexOf(list, kv.Value) >= 0 && Array.IndexOf(list, kv.Key) < 0)
                {
                    if (extra == null)
                        extra = new List<AircraftDefinition>(2);
                    extra.Add(kv.Key);
                }
            }
            return extra;
        }

        // Hangar.availableAircraft is non-public in the game assembly (BOTE builds against a
        // publicized copy), so go through reflection.
        private static readonly FieldInfo HangarAircraft = AccessTools.Field(typeof(Hangar), "availableAircraft");

        internal static bool Present { get { return _modAssetsType != null; } }

        /// <summary>Called from Awake. BOTE initialises after Blueprinter is ready and fires ModAssets.OnInitialize.</summary>
        internal static void Hook()
        {
            if (!SubConfig.BoteEnabled.Value)
                return;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("BoscaliOceanTrainingExercise.ModAssets", false);
                if (t != null)
                {
                    _modAssetsType = t;
                    break;
                }
            }
            if (_modAssetsType == null)
                return;

            FieldInfo evt = AccessTools.Field(_modAssetsType, "OnInitialize");
            if (evt == null || evt.FieldType != typeof(Action<bool>))
            {
                SubmarinesPlugin.Log.LogWarning("BOTE found but its ModAssets.OnInitialize hook has changed; player subs disabled.");
                return;
            }
            Action<bool> current = evt.GetValue(null) as Action<bool>;
            evt.SetValue(null, (Action<bool>)Delegate.Combine(current, new Action<bool>(OnBoteInitialized)));
            SubmarinesPlugin.Log.LogInfo("BOTE detected: player-drivable submarines will be added to its docks.");
        }

        /// <summary>A BOTE player vessel (ship flown like an aircraft).</summary>
        internal static bool IsBoteShip(UnitDefinition def)
        {
            AircraftDefinition a = def as AircraftDefinition;
            return a != null && _shipDefs != null && _shipDefs.Contains(a);
        }

        /// <summary>Anything that floats: stock ships, our subs, BOTE player vessels.</summary>
        internal static bool IsVessel(Unit u)
        {
            if (u == null)
                return false;
            if (u is Ship)
                return true;
            if (u.GetComponent<Runtime.Submarine>() != null)
                return true;
            return IsBoteShip(u.definition);
        }

        private static void OnBoteInitialized(bool first)
        {
            try
            {
                object assets = AccessTools.Property(_modAssetsType, "i")?.GetValue(null, null);
                if (assets == null)
                    return;
                _shipDefs = AccessTools.Field(_modAssetsType, "ShipDefinitions")?.GetValue(assets) as ICollection<AircraftDefinition>;
                if (_shipDefs == null)
                {
                    SubmarinesPlugin.Log.LogWarning("BOTE ShipDefinitions not found; player subs disabled.");
                    return;
                }
                BuildingDefinition dock = AccessTools.Field(_modAssetsType, "dockDef")?.GetValue(assets) as BuildingDefinition;
                Register(dock);
            }
            catch (Exception ex)
            {
                SubmarinesPlugin.Log.LogError("BOTE integration failed: " + ex);
            }
        }

        private static void Register(BuildingDefinition dock)
        {
            Encyclopedia enc = Encyclopedia.i;
            if (enc == null)
                return;

            LogBoteShips();
            AircraftDefinition attackDonor = PickDonor(SubConfig.Keys(SubConfig.BoteAttackDonorKeys), null);
            AircraftDefinition ballisticDonor = PickDonor(SubConfig.Keys(SubConfig.BoteBallisticDonorKeys), attackDonor) ?? attackDonor;
            if (attackDonor == null)
            {
                SubmarinesPlugin.Log.LogWarning("No BOTE ship found to build player subs from. Check [BOTE] donor keys.");
                return;
            }

            AircraftDefinition ssn = RegisterOne(enc, SubSpec.Attack, attackDonor,
                SubRegistry.Price(attackDonor, SubConfig.AttackCostMultiplier.Value));
            AircraftDefinition ssbn = RegisterOne(enc, SubSpec.Ballistic, ballisticDonor,
                SubRegistry.Price(ballisticDonor, SubConfig.BallisticCostMultiplier.Value));

            // BOTE re-runs its init when Blueprinter reloads; re-add every time, it's idempotent.
            if (ssn != null)
            {
                DonorOf[ssn] = attackDonor;
                _shipDefs.Add(ssn);
                OfferWherever(attackDonor, ssn, dock);
            }
            if (ssbn != null)
            {
                DonorOf[ssbn] = ballisticDonor;
                _shipDefs.Add(ssbn);
                OfferWherever(ballisticDonor, ssbn, dock);
            }
            if (!_registered)
            {
                SubRegistry.RegisterWithMirage();
                _registered = true;
            }
        }

        private static AircraftDefinition RegisterOne(Encyclopedia enc, SubSpec spec, AircraftDefinition donor, float cost)
        {
            UnitDefinition existing;
            if (Encyclopedia.Lookup != null && Encyclopedia.Lookup.TryGetValue(spec.PlayerJsonKey, out existing))
                return existing as AircraftDefinition;

            AircraftDefinition def = UnityEngine.Object.Instantiate(donor);
            def.name = spec.PlayerJsonKey + "_Definition";
            def.jsonKey = spec.PlayerJsonKey;
            def.unitName = spec.UnitName;
            def.code = spec.Code;
            def.description = spec.Description + " Player-driven (BOTE): dive with " + SubConfig.DeeperKey.Value +
                              ", climb with " + SubConfig.ShallowerKey.Value + ", F8 for sonar.";
            def.value = cost;
            def.radarSize = Mathf.Max(0.5f, donor.radarSize * 0.15f);
            SubRegistry.MarkEnabled(def);
            if (!SubRegistry.BuildSubPrefab(def, donor.unitPrefab, spec, spec.PlayerJsonKey))
                return null;

            if (enc.aircraft == null)
                enc.aircraft = new List<AircraftDefinition>();
            enc.aircraft.Add(def);
            SubRegistry.AddToLookups(enc, def);
            SubmarinesPlugin.Log.LogInfo("Registered player " + spec.UnitName + " (" + spec.PlayerJsonKey + ") on BOTE's '" +
                                         donor.unitName + "' [" + donor.jsonKey + "], price " + cost.ToString("F0") + ".");
            return def;
        }

        /// <summary>Add the sub to every hangar that offers its donor ship, plus BOTE's dock.</summary>
        private static void OfferWherever(AircraftDefinition donor, AircraftDefinition sub, BuildingDefinition dock)
        {
            int added = 0;
            List<Hangar> hangars = new List<Hangar>(Resources.FindObjectsOfTypeAll<Hangar>());
            if (dock != null && dock.unitPrefab != null)
            {
                Hangar dh = dock.unitPrefab.GetComponentInChildren<Hangar>(true);
                if (dh != null && !hangars.Contains(dh))
                    hangars.Add(dh);
            }
            if (HangarAircraft == null)
            {
                SubmarinesPlugin.Log.LogWarning("Hangar.availableAircraft not found; player subs can't be offered at docks.");
                return;
            }
            foreach (Hangar h in hangars)
            {
                if (h == null)
                    continue;
                AircraftDefinition[] list = HangarAircraft.GetValue(h) as AircraftDefinition[];
                if (list == null)
                    continue;
                bool isDock = dock != null && dock.unitPrefab != null && h.transform.IsChildOf(dock.unitPrefab.transform);
                if (Array.IndexOf(list, sub) >= 0)
                    continue;
                if (!isDock && Array.IndexOf(list, donor) < 0)
                    continue;
                HangarAircraft.SetValue(h, list.AddToArray(sub));
                added++;
            }
            SubmarinesPlugin.Log.LogInfo(sub.unitName + " offered at " + added + " hangar(s)/dock(s).");
        }

        private static AircraftDefinition PickDonor(string[] keys, AircraftDefinition avoid)
        {
            for (int k = 0; k < keys.Length; k++)
            {
                if (keys[k].Length == 0)
                    continue;
                foreach (AircraftDefinition d in _shipDefs)
                {
                    if (d == null || d == avoid || d.unitPrefab == null || SubSpec.ForKey(d.jsonKey) != null)
                        continue;
                    string hay = (d.jsonKey + " " + d.unitName + " " + d.name).ToLowerInvariant();
                    if (hay.Contains(keys[k]))
                        return d;
                }
            }
            return null;
        }

        private static void LogBoteShips()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("BOTE player ships (jsonKey | name):");
            foreach (AircraftDefinition d in _shipDefs)
            {
                if (d != null)
                    sb.Append("\n  ").Append(d.jsonKey).Append(" | ").Append(d.unitName);
            }
            SubmarinesPlugin.Log.LogInfo(sb.ToString());
        }
    }
}
