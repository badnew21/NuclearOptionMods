using HarmonyLib;
using NuclearOption.Jobs;
using NuclearOption.Networking;
using Submarines.Bootstrap;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.Patches
{
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad")]
    internal static class EncyclopediaAfterLoadPatch
    {
        private static void Postfix(Encyclopedia __instance)
        {
            SubRegistry.OnEncyclopediaLoaded(__instance);
        }
    }

    [HarmonyPatch(typeof(NetworkManagerNuclearOption), "RegisterPrefabs")]
    internal static class RegisterPrefabsPatch
    {
        private static void Postfix()
        {
            SubRegistry.RegisterWithMirage();
        }
    }

    /// <summary>Attach submarine systems and hull sonar when a ship comes to life.</summary>
    [HarmonyPatch(typeof(Unit), "InitializeUnit")]
    internal static class UnitInitializePatch
    {
        private static void Postfix(Unit __instance)
        {
            Ship ship = __instance as Ship;
            if (ship == null)
                return;
            try
            {
                SubSpec spec = SubRegistry.SpecOf(ship);
                if (spec != null)
                {
                    Submarine.Attach(ship, spec);
                    return;
                }
                if (SubConfig.SurfaceShipsGetSonar.Value && ship.definition != null && !SubRegistry.IsExcludedHull(ship.definition))
                    SonarArray.Attach(ship, null);
            }
            catch (System.Exception ex)
            {
                SubmarinesPlugin.Log.LogError("Attaching sub systems failed: " + ex);
            }
        }
    }

    internal static class Stealth
    {
        /// <summary>
        /// Should a sensor on <paramref name="sensorObj"/> be allowed to see <paramref name="target"/>?
        /// </summary>
        internal static bool Allow(object sensorObj, Unit target, bool radar)
        {
            if (UnderwaterWorld.Subs.Count == 0)
                return true;

            Component sensor = sensorObj as Component;
            if (sensor != null)
            {
                // A submerged boat's own radar / optics are under water.
                Submarine own = sensor.GetComponentInParent<Submarine>();
                if (own != null && !own.MastsExposed)
                    return false;
            }

            if (target == null)
                return true;
            Submarine sub = target.GetComponent<Submarine>();
            if (sub == null || !sub.Submerged)
                return true;

            // Fully under: no radar return, nothing to see.
            if (!sub.MastsExposed)
                return false;

            // Periscope depth: only the masts are up, and only close sensors can pick them out.
            float range = radar ? SubConfig.MastRadarRange.Value : SubConfig.MastVisualRange.Value;
            if (range <= 0f || sensor == null)
                return false;
            return (sensor.transform.position - target.transform.position).sqrMagnitude < range * range;
        }
    }

    [HarmonyPatch(typeof(DetectorManager), "RequestRadarCheck")]
    internal static class RadarCheckPatch
    {
        private static bool Prefix(TargetDetector detector, Unit target)
        {
            return Stealth.Allow(detector, target, true);
        }
    }

    [HarmonyPatch(typeof(DetectorManager), "RequestLoSCheck")]
    internal static class LoSCheckPatch
    {
        private static bool Prefix(TargetDetector detector, Unit target)
        {
            return Stealth.Allow(detector, target, false);
        }
    }

    /// <summary>Turrets never slew onto a submerged boat, and a submerged boat's deck guns stay quiet.</summary>
    [HarmonyPatch(typeof(Turret), "Turret_OnDetectTarget")]
    internal static class TurretDetectPatch
    {
        private static bool Prefix(Turret __instance, Unit unit)
        {
            if (UnderwaterWorld.Subs.Count == 0)
                return true;
            Component self = (object)__instance as Component;
            if (self != null)
            {
                Submarine own = self.GetComponentInParent<Submarine>();
                if (own != null && own.Submerged)
                    return false;
            }
            if (unit != null)
            {
                Submarine sub = unit.GetComponent<Submarine>();
                if (sub != null && sub.Submerged)
                    return false;
            }
            return true;
        }
    }
}
