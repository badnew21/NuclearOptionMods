using System.Collections.Generic;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.Visual
{
    /// <summary>
    /// Draws every torpedo in the water (the server's own, or the client mirrors kept in sync by
    /// SubNet) as a 6.4 m torpedo model.
    /// </summary>
    internal static class TorpedoVisuals
    {
        internal const string Name = "SubmarineTorpedo";

        private static readonly Dictionary<int, GameObject> Live = new Dictionary<int, GameObject>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static Material[] _mats;

        internal static void Tick()
        {
            List<Torpedo> torps = UnderwaterWorld.Torpedoes;
            if (torps.Count == 0 && Live.Count == 0)
                return;

            Seen.Clear();
            for (int i = 0; i < torps.Count; i++)
            {
                Torpedo t = torps[i];
                if (t.Dead)
                    continue;
                Seen.Add(t.Id);
                GameObject go;
                if (!Live.TryGetValue(t.Id, out go) || go == null)
                {
                    go = Create();
                    Live[t.Id] = go;
                }
                go.transform.position = Geo.ToLocal(t.Pos);
                if (t.Heading.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.LookRotation(t.Heading);
            }

            Gone.Clear();
            foreach (KeyValuePair<int, GameObject> kv in Live)
            {
                if (!Seen.Contains(kv.Key))
                    Gone.Add(kv.Key);
            }
            for (int i = 0; i < Gone.Count; i++)
            {
                GameObject go = Live[Gone[i]];
                if (go != null)
                    Object.Destroy(go);
                Live.Remove(Gone[i]);
            }
        }

        private static GameObject Create()
        {
            GameObject go = new GameObject(Name);
            go.AddComponent<MeshFilter>().sharedMesh = SubVisual.TorpedoMesh();
            if (_mats == null)
            {
                Submarine any = UnderwaterWorld.Subs.Count > 0 ? UnderwaterWorld.Subs[0] : null;
                _mats = SubVisual.Materials(any != null ? any.gameObject : null);
            }
            go.AddComponent<MeshRenderer>().sharedMaterials = _mats;
            return go;
        }
    }
}
