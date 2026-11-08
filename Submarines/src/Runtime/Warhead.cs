using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using Submarines.Bootstrap;
using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>
    /// Torpedo warhead effects (server). Damage is applied straight to the hull parts in the bubble
    /// radius; underwater blasts are far more destructive than air bursts, hence the structure term.
    /// For visuals a stock missile is spawned at the impact point and detonated, so every client sees
    /// a networked explosion.
    /// </summary>
    internal static class Warhead
    {
        private static readonly Collider[] Buf = new Collider[256];
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly FieldInfo WarheadField = AccessTools.Field(typeof(Missile), "warhead");
        private static readonly FieldInfo BlastYieldField = AccessTools.Field(typeof(Missile), "blastYield");

        internal static void Detonate(Vector3 localPos, Unit victim, Unit owner, PersistentID dealer, float yieldKg)
        {
            if (!Net.IsServer)
                return;

            FactionHQ ownerHq = owner != null ? owner.NetworkHQ : null;
            float power = Mathf.Pow(Mathf.Max(yieldKg, 1f), 1f / 3f);
            float radius = power * 9f;

            Seen.Clear();
            int n = Physics.OverlapSphereNonAlloc(localPos, radius, Buf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = Buf[i];
                if (col == null)
                    continue;
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null)
                    continue;
                Unit u = dmg.GetUnit();
                if (u == null || u.disabled)
                    continue;
                if (ownerHq != null && u.NetworkHQ == ownerHq && u != victim)
                    continue;
                if (!Seen.Add(col.GetInstanceID()))
                    continue;

                float dist = Vector3.Distance(col.ClosestPoint(localPos), localPos);
                float prox = 1f - Mathf.Clamp01(dist / radius);
                if (prox <= 0f)
                    continue;
                // Overpressure term mirrors the stock blast model; structure term is the keel-breaking
                // pressure pulse that makes torpedoes lethal.
                float overpressure = yieldKg * 6f * prox * prox;
                float structure = yieldKg * 8f * prox * prox;
                try { dmg.TakeDamage(0f, overpressure, prox, 0f, structure, dealer); }
                catch (System.Exception ex)
                {
                    if (SubConfig.VerboseLog.Value)
                        SubmarinesPlugin.Log.LogWarning("TakeDamage failed: " + ex.Message);
                }
            }

            bool visual = SubConfig.StockWarheadVisuals.Value && SpawnVisual(localPos, victim, owner, yieldKg);
            if (!visual)
            {
                try { DamageEffects.BlastFrag(yieldKg, localPos, dealer, PersistentID.None); }
                catch (System.Exception ex) { SubmarinesPlugin.Log.LogWarning("BlastFrag failed: " + ex.Message); }
            }
        }

        private static bool SpawnVisual(Vector3 localPos, Unit victim, Unit owner, float yieldKg)
        {
            if (owner == null || owner.disabled || SubmarinesPlugin.Instance == null)
                return false;
            Spawner spawner = NetworkSceneSingleton<Spawner>.i;
            MissileDefinition def = SubRegistry.FindMissile(SubConfig.Keys(SubConfig.WarheadVisualMissileKeys));
            if (spawner == null || def == null)
                return false;

            // Put the burst at the waterline so the column of water is visible.
            Vector3 p = localPos;
            p.y = Mathf.Max(p.y, Geo.Sea - 2f);
            Missile m;
            try { m = spawner.SpawnMissile(def, p, Quaternion.LookRotation(Vector3.down), Vector3.zero, victim, owner); }
            catch (System.Exception ex)
            {
                SubmarinesPlugin.Log.LogWarning("Visual warhead spawn failed: " + ex.Message);
                return false;
            }
            if (m == null)
                return false;
            SubmarinesPlugin.Instance.StartCoroutine(DetonateNextFrames(m, yieldKg));
            return true;
        }

        private static IEnumerator DetonateNextFrames(Missile m, float yieldKg)
        {
            yield return null;
            yield return null;
            if (m == null || m.disabled)
                yield break;
            try
            {
                if (BlastYieldField != null)
                    BlastYieldField.SetValue(m, yieldKg);
                object raw = WarheadField != null ? WarheadField.GetValue(m) : null;
                if (raw is Missile.Warhead wh)
                    wh.Armed = true;
                m.Detonate(Vector3.up, false, false);
            }
            catch (System.Exception ex)
            {
                SubmarinesPlugin.Log.LogWarning("Visual warhead detonate failed: " + ex.Message);
            }
        }
    }
}
