using HarmonyLib;
using NuclearOption.Jobs;
using NuclearOption.Networking;
using Submarines.Bootstrap;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.Patches
{
    // AfterLoad is overloaded (AfterLoad() and AfterLoad(Encyclopedia)); name the parameterless
    // one explicitly like Blueprinter does, or Harmony rejects the patch as ambiguous.
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new System.Type[0])]
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

    /// <summary>Attach submarine systems and hull sonar when a ship (or BOTE player vessel) comes to life.</summary>
    [HarmonyPatch(typeof(Unit), "InitializeUnit")]
    internal static class UnitInitializePatch
    {
        private static void Postfix(Unit __instance)
        {
            Unit unit = __instance;
            if (unit == null)
                return;
            try
            {
                SubSpec spec = SubRegistry.SpecOf(unit);
                if (spec != null)
                {
                    Submarine.Attach(unit, spec);
                    return;
                }
                if (!SubConfig.SurfaceShipsGetSonar.Value || unit.definition == null || SubRegistry.IsExcludedHull(unit.definition))
                    return;
                if (unit is Ship)
                {
                    SonarArray.Attach(unit, null, SubConfig.SurfaceAsw.Value ? SubConfig.SurfaceAswTorpedoes.Value : 0);
                }
                else if (BoteBridge.IsBoteShip(unit.definition))
                {
                    // Player-driven BOTE warship: sonar so its pilot can hunt subs on the F8 display.
                    // No automatic ASW torpedoes; a human is in charge of that ship.
                    SonarArray.Attach(unit, null, 0);
                }
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
