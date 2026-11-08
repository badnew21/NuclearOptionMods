using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Submarines.Bootstrap
{
    /// <summary>
    /// Removes the donor ship's surface weapons (deck guns, CIWS, missile launchers) from a
    /// submarine prefab, leaving only submarine weapons: the mod's torpedoes, decoys and, on the
    /// SSBN, SLBMs.
    ///
    /// Done on the inactive template before anything has started, without deleting anything:
    ///  - turret weapons register their own weapon station when the turret starts, so the turret
    ///    objects are switched off and never start (the objects stay, so references to them from
    ///    the damage model remain valid);
    ///  - loadout weapons come from WeaponManager hardpoint options, which are reduced to the empty
    ///    option (a null entry, the game's own "empty" convention).
    /// </summary>
    internal static class SubArmament
    {
        private static readonly FieldInfo HardpointSetsField = AccessTools.Field(typeof(WeaponManager), "hardpointSets");

        internal static void Strip(GameObject template, string label)
        {
            if (template == null || !SubConfig.StripDonorWeapons.Value)
                return;
            int turrets = 0, launchers = 0, hardpoints = 0;

            foreach (Turret t in template.GetComponentsInChildren<Turret>(true))
            {
                if (t != null && Disable(template, t.gameObject))
                    turrets++;
            }

            // Fixed launchers/guns that aren't on a turret (ship VLS cells and the like).
            foreach (Weapon w in template.GetComponentsInChildren<Weapon>(true))
            {
                if (w == null || w.GetComponentInParent<Turret>() != null)
                    continue;
                if (Disable(template, w.gameObject))
                    launchers++;
            }

            foreach (WeaponManager wm in template.GetComponentsInChildren<WeaponManager>(true))
            {
                HardpointSet[] sets = wm != null && HardpointSetsField != null ? HardpointSetsField.GetValue(wm) as HardpointSet[] : null;
                if (sets == null)
                    continue;
                foreach (HardpointSet set in sets)
                {
                    if (set == null)
                        continue;
                    set.weaponOptions = new List<WeaponMount> { null };
                    hardpoints++;
                }
            }

            SubmarinesPlugin.Log.LogInfo(label + ": removed donor weapons (" + turrets + " turret(s), " + launchers +
                                         " fixed launcher(s), " + hardpoints + " hardpoint(s) emptied).");
        }

        /// <summary>
        /// Switch off a weapon's object, unless it holds the unit itself or anything the hull needs
        /// to work (physics, the unit, the weapon manager, our model).
        /// </summary>
        private static bool Disable(GameObject template, GameObject go)
        {
            if (go == null || go == template || !go.activeSelf)
                return false;
            if (go.GetComponentInChildren<Unit>(true) != null || go.GetComponentInChildren<Rigidbody>(true) != null ||
                go.GetComponentInChildren<WeaponManager>(true) != null)
                return false;
            if (go.transform.Find(Visual.SubVisual.ModelName) != null)
                return false;
            go.SetActive(false);
            return true;
        }
    }
}
