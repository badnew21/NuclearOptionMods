using NuclearOption.Networking;
using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>
    /// Position helpers. The game uses a floating origin, so anything we simulate ourselves
    /// (torpedoes, decoys, false echoes) is stored in "global" space: local minus Datum.origin.
    /// </summary>
    internal static class Geo
    {
        internal static float Sea { get { return Datum.LocalSeaY; } }

        internal static Vector3 ToGlobal(Vector3 local) { return local - Datum.origin.position; }
        internal static Vector3 ToLocal(Vector3 global) { return global + Datum.origin.position; }

        internal static Vector3 GlobalOf(Unit u) { return u.GlobalPosition().AsVector3(); }

        /// <summary>Metres below the surface (positive = underwater) of a local-space point.</summary>
        internal static float DepthLocal(Vector3 local) { return Sea - local.y; }

        /// <summary>Depth of a global-space point.</summary>
        internal static float DepthGlobal(Vector3 global) { return DepthLocal(ToLocal(global)); }

        internal static float DepthOf(Unit u) { return u != null ? DepthLocal(u.transform.position) : 0f; }

        /// <summary>Water depth under a local point. Returns 5000 for open ocean with no seabed hit.</summary>
        internal static float WaterDepthLocal(Vector3 local)
        {
            Vector3 from = new Vector3(local.x, Sea + 10f, local.z);
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, 6000f, PhysicsLayers.StaticsMask.value, QueryTriggerInteraction.Ignore))
                return Mathf.Max(0f, Sea - hit.point.y);
            return 5000f;
        }

        internal static float Bearing(Vector3 fromGlobal, Vector3 toGlobal)
        {
            Vector3 d = toGlobal - fromGlobal;
            float b = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return b < 0f ? b + 360f : b;
        }

        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    internal static class Net
    {
        internal static bool IsServer
        {
            get
            {
                NetworkManagerNuclearOption nm = NetworkManagerNuclearOption.i;
                return nm != null && nm.Server != null && nm.Server.Active;
            }
        }

        /// <summary>False in the encyclopedia viewer, where units are on display rather than in play.</summary>
        internal static bool InPlay { get { return GameManager.gameState != GameState.Encyclopedia; } }

        internal static bool Hostile(FactionHQ a, FactionHQ b)
        {
            return a != null && b != null && a != b;
        }
    }
}
