using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Mirage;
using UnityEngine;

namespace Submarines.Bootstrap
{
    /// <summary>
    /// Builds the two submarine ShipDefinitions and their network prefabs at Encyclopedia.AfterLoad.
    ///
    /// Each sub gets its OWN prefab, cloned from a stock warship hull, so that spawned units carry our
    /// definition (and therefore our jsonKey) and can be told apart from the donor ship everywhere:
    /// mission editor, save files, multiplayer spawn messages.
    ///
    /// The clone lives under an inactive holder object. That keeps Awake/OnEnable from ever running on
    /// the template, while Instantiate() of it (no parent) still yields a normal, active instance.
    /// </summary>
    internal static class SubRegistry
    {
        private static bool _done;
        private static GameObject _holder;
        private static readonly Dictionary<string, UnitDefinition> Defs = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);

        private static readonly FieldInfo UnitDefDisabled = AccessTools.Field(typeof(UnitDefinition), "disabled");
        private static readonly FieldInfo SpawnedFromInstantiate =
            AccessTools.Field(typeof(NetworkIdentity), "<SpawnedFromInstantiate>k__BackingField");

        internal static IEnumerable<UnitDefinition> Definitions { get { return Defs.Values; } }

        internal static void OnEncyclopediaLoaded(Encyclopedia enc)
        {
            if (_done || enc == null)
                return;
            try
            {
                LogCatalog(enc);
                ShipDefinition attackDonor = PickDonor(enc, SubConfig.Keys(SubConfig.AttackDonorKeys), null);
                ShipDefinition ballisticDonor = PickDonor(enc, SubConfig.Keys(SubConfig.BallisticDonorKeys), attackDonor);
                if (ballisticDonor == null)
                    ballisticDonor = attackDonor;

                if (attackDonor == null)
                {
                    SubmarinesPlugin.Log.LogError("No usable donor ship hull found; submarines not registered.");
                    return;
                }

                Register(enc, SubSpec.Attack, attackDonor, SubConfig.AttackCost.Value);
                Register(enc, SubSpec.Ballistic, ballisticDonor, SubConfig.BallisticCost.Value);
                RegisterWithMirage();
                _done = true;
            }
            catch (Exception ex)
            {
                SubmarinesPlugin.Log.LogError("Submarine bootstrap failed: " + ex);
            }
        }

        private static void Register(Encyclopedia enc, SubSpec spec, ShipDefinition donor, float cost)
        {
            ShipDefinition existing;
            if (TryGetShip(spec.JsonKey, out existing))
            {
                // Another loader (or a reload) already registered it.
                Defs[spec.JsonKey] = existing;
                return;
            }

            // Copying the asset keeps every ShipDefinition field we do not know about (AI, nav, icons).
            ShipDefinition def = UnityEngine.Object.Instantiate(donor);
            def.name = spec.JsonKey + "_Definition";
            def.jsonKey = spec.JsonKey;
            def.unitName = spec.UnitName;
            def.code = spec.Code;
            def.description = spec.Description;
            def.value = cost;
            // Submerged RCS is handled by the detection patches; this is the surfaced figure.
            def.radarSize = Mathf.Max(0.5f, donor.radarSize * 0.15f);
            if (UnitDefDisabled != null)
                UnitDefDisabled.SetValue(def, false);

            if (!BuildSubPrefab(def, donor.unitPrefab, spec, spec.JsonKey))
                return;

            if (enc.ships == null)
                enc.ships = new List<ShipDefinition>();
            enc.ships.Add(def);
            AddToLookups(enc, def);
            SubmarinesPlugin.Log.LogInfo("Registered " + spec.UnitName + " (" + spec.JsonKey + ") on the '" +
                                         donor.unitName + "' hull [" + donor.jsonKey + "].");
        }

        /// <summary>Clone the donor prefab, point it at the new definition and give it the sub model.</summary>
        internal static bool BuildSubPrefab(UnitDefinition def, GameObject donorPrefab, SubSpec spec, string key)
        {
            GameObject prefab = ClonePrefab(donorPrefab, key);
            if (prefab == null)
            {
                SubmarinesPlugin.Log.LogError("Could not clone prefab for " + key);
                return false;
            }
            def.unitPrefab = prefab;
            Visual.HullPlan plan = Visual.SubVisual.PlanFor(spec);
            def.length = plan.Length;
            def.width = plan.Beam;
            def.height = plan.Height;
            Visual.SubVisual.Apply(prefab, spec);
            Unit unit = prefab.GetComponent<Unit>();
            if (unit != null)
                unit.definition = def;
            return true;
        }

        /// <summary>Encyclopedia lookup tables, network index and mass cache for a new definition.</summary>
        internal static void AddToLookups(Encyclopedia enc, UnitDefinition def)
        {
            if (Encyclopedia.Lookup == null)
                Encyclopedia.Lookup = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            Encyclopedia.Lookup[def.jsonKey] = def;

            if (enc.IndexLookup == null)
                enc.IndexLookup = new List<INetworkDefinition>();
            ((INetworkDefinition)def).LookupIndex = enc.IndexLookup.Count;
            enc.IndexLookup.Add(def);

            try { def.CacheMass(); }
            catch (Exception ex) { SubmarinesPlugin.Log.LogWarning("CacheMass failed for " + def.jsonKey + ": " + ex.Message); }

            Defs[def.jsonKey] = def;
        }

        private static GameObject ClonePrefab(GameObject source, string key)
        {
            if (source == null)
                return null;
            if (_holder == null)
            {
                _holder = new GameObject("Submarines_PrefabHolder");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }

            GameObject clone = UnityEngine.Object.Instantiate(source, _holder.transform, false);
            clone.name = key;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // Only hashes every peer agrees on (encyclopedia prefabs), so host and clients pick the
            // same hash for the clone and the client can spawn what the host sends.
            HashSet<int> taken = new HashSet<int>();
            if (Encyclopedia.Lookup != null)
            {
                foreach (UnitDefinition u in Encyclopedia.Lookup.Values)
                {
                    if (u == null || u.unitPrefab == null || u.unitPrefab == clone)
                        continue;
                    foreach (NetworkIdentity ni in u.unitPrefab.GetComponentsInChildren<NetworkIdentity>(true))
                    {
                        if (ni != null && ni.PrefabHash != 0)
                            taken.Add(ni.PrefabHash);
                    }
                }
            }

            NetworkIdentity[] ids = clone.GetComponentsInChildren<NetworkIdentity>(true);
            int hash = StableHash("submarines|" + key);
            for (int i = 0; i < ids.Length; i++)
            {
                NetworkIdentity id = ids[i];
                id.ClearSceneId();
                if (SpawnedFromInstantiate != null)
                    SpawnedFromInstantiate.SetValue(id, false);
                while (hash == 0 || taken.Contains(hash))
                    hash++;
                id.PrefabHash = hash;
                taken.Add(hash);
            }
            return clone;
        }

        /// <summary>
        /// Make sure Mirage's client side knows the prefab hash so remote clients can spawn the sub.
        /// Harmless if the game already registered it from the Encyclopedia.
        /// </summary>
        internal static void RegisterWithMirage()
        {
            Type com = typeof(NetworkIdentity).Assembly.GetType("Mirage.ClientObjectManager");
            if (com == null)
                return;
            MethodInfo reg = AccessTools.Method(com, "RegisterPrefab", new[] { typeof(NetworkIdentity) });
            if (reg == null)
                return;
            foreach (UnityEngine.Object mgr in Resources.FindObjectsOfTypeAll(com))
            {
                foreach (UnitDefinition def in Defs.Values)
                {
                    if (def == null || def.unitPrefab == null)
                        continue;
                    NetworkIdentity ni = def.unitPrefab.GetComponent<NetworkIdentity>();
                    if (ni == null)
                        continue;
                    try { reg.Invoke(mgr, new object[] { ni }); }
                    catch (Exception ex)
                    {
                        if (SubConfig.VerboseLog.Value)
                            SubmarinesPlugin.Log.LogDebug("RegisterPrefab " + def.jsonKey + ": " + ex.InnerException);
                    }
                }
            }
        }

        private static ShipDefinition PickDonor(Encyclopedia enc, string[] keys, ShipDefinition avoid)
        {
            if (enc.ships == null)
                return null;
            for (int k = 0; k < keys.Length; k++)
            {
                if (keys[k].Length == 0)
                    continue;
                ShipDefinition pick = null;
                foreach (ShipDefinition s in enc.ships)
                {
                    if (!Usable(s) || s == avoid)
                        continue;
                    string hay = (s.jsonKey + " " + s.unitName + " " + s.name).ToLowerInvariant();
                    if (hay.Contains(keys[k]))
                    {
                        pick = s;
                        break;
                    }
                }
                if (pick != null)
                    return pick;
            }

            // Fallback: the smallest warship that is not a landing craft / patrol boat / carrier.
            ShipDefinition best = null;
            foreach (ShipDefinition s in enc.ships)
            {
                if (!Usable(s) || s == avoid || IsExcludedHull(s))
                    continue;
                if (best == null || s.length < best.length)
                    best = s;
            }
            return best;
        }

        private static bool Usable(ShipDefinition s)
        {
            return s != null && s.unitPrefab != null && SubSpec.ForKey(s.jsonKey) == null;
        }

        internal static void MarkEnabled(UnitDefinition def)
        {
            if (UnitDefDisabled != null)
                UnitDefDisabled.SetValue(def, false);
        }

        internal static bool IsExcludedHull(UnitDefinition s)
        {
            string hay = (s.jsonKey + " " + s.unitName).ToLowerInvariant();
            return hay.Contains("landing") || hay.Contains("otb") || hay.Contains("patrol") ||
                   hay.Contains("carrier") || hay.Contains("cargo") || hay.Contains("tanker");
        }

        private static bool TryGetShip(string key, out ShipDefinition def)
        {
            def = null;
            UnitDefinition u;
            if (Encyclopedia.Lookup != null && Encyclopedia.Lookup.TryGetValue(key, out u))
                def = u as ShipDefinition;
            return def != null && def.unitPrefab != null;
        }

        private static void LogCatalog(Encyclopedia enc)
        {
            StringBuilder sb = new StringBuilder("Stock ships (jsonKey | name | length):");
            if (enc.ships != null)
            {
                foreach (ShipDefinition s in enc.ships)
                {
                    if (s != null)
                        sb.Append("\n  ").Append(s.jsonKey).Append(" | ").Append(s.unitName).Append(" | ").Append(s.length.ToString("F0")).Append(" m");
                }
            }
            sb.Append("\nStock missiles (jsonKey | name):");
            if (enc.missiles != null)
            {
                foreach (MissileDefinition m in enc.missiles)
                {
                    if (m != null)
                        sb.Append("\n  ").Append(m.jsonKey).Append(" | ").Append(m.unitName);
                }
            }
            SubmarinesPlugin.Log.LogInfo(sb.ToString());
        }

        internal static MissileDefinition FindMissile(string[] keys)
        {
            Encyclopedia enc = Encyclopedia.i;
            if (enc == null || enc.missiles == null)
                return null;
            for (int k = 0; k < keys.Length; k++)
            {
                if (keys[k].Length == 0)
                    continue;
                foreach (MissileDefinition m in enc.missiles)
                {
                    if (m == null || m.unitPrefab == null)
                        continue;
                    string hay = (m.jsonKey + " " + m.unitName).ToLowerInvariant();
                    if (hay.Contains(keys[k]))
                        return m;
                }
            }
            return null;
        }

        internal static SubSpec SpecOf(Unit u)
        {
            if (u == null || u.definition == null)
                return null;
            return SubSpec.ForKey(u.definition.jsonKey);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619;
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
