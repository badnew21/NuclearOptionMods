using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>Registry and ticker for everything that lives under the surface.</summary>
    internal static class UnderwaterWorld
    {
        internal static readonly List<Submarine> Subs = new List<Submarine>();
        internal static readonly List<SonarArray> Arrays = new List<SonarArray>();
        internal static readonly List<Decoy> Decoys = new List<Decoy>();
        internal static readonly List<Torpedo> Torpedoes = new List<Torpedo>();

        internal static void Register(Submarine s) { if (!Subs.Contains(s)) Subs.Add(s); }
        internal static void Unregister(Submarine s) { Subs.Remove(s); }
        internal static void Register(SonarArray a) { if (!Arrays.Contains(a)) Arrays.Add(a); }
        internal static void Unregister(SonarArray a) { Arrays.Remove(a); }

        internal static void Tick()
        {
            if (Subs.Count == 0 && Arrays.Count == 0)
            {
                Decoys.Clear();
                Torpedoes.Clear();
                return;
            }

            Subs.RemoveAll(s => s == null);
            Arrays.RemoveAll(a => a == null);

            float dt = Time.deltaTime;
            bool server = Net.IsServer;
            for (int i = Decoys.Count - 1; i >= 0; i--)
            {
                Decoy d = Decoys[i];
                if (!d.Alive)
                {
                    Decoys.RemoveAt(i);
                    if (server)
                        Sync.SubNet.SendDecoyEnd(d);
                    continue;
                }
                d.Tick(dt);
            }

            // Server runs the real torpedoes; clients dead-reckon their mirrors.
            for (int i = Torpedoes.Count - 1; i >= 0; i--)
            {
                Torpedo t = Torpedoes[i];
                if (server == t.Remote)
                {
                    // A mirror left over after becoming host (or vice versa): drop it.
                    Torpedoes.RemoveAt(i);
                    continue;
                }
                t.Tick(dt);
                if (t.Dead)
                {
                    Torpedoes.RemoveAt(i);
                    if (server)
                        Sync.SubNet.SendTorpedoEnd(t);
                }
            }
        }

        internal static Torpedo FindTorpedo(int id)
        {
            for (int i = 0; i < Torpedoes.Count; i++)
            {
                if (Torpedoes[i].Id == id)
                    return Torpedoes[i];
            }
            return null;
        }

        internal static Decoy FindDecoy(int id)
        {
            for (int i = 0; i < Decoys.Count; i++)
            {
                if (Decoys[i].Id == id)
                    return Decoys[i];
            }
            return null;
        }

        internal static Torpedo LaunchTorpedo(Unit owner, SonarArray array, SonarContact target, bool asw)
        {
            if (!Net.IsServer || owner == null || target == null)
                return null;
            Torpedo t = new Torpedo(owner, array, target, target.EstimatedPos, asw);
            Torpedoes.Add(t);
            // Launch transient: everyone nearby hears the tube flood and the motor start.
            Transient(owner, 25000f);
            if (SubConfig.VerboseLog.Value)
                SubmarinesPlugin.Log.LogInfo(owner.unitName + " fired torpedo " + t.Id + " at a " +
                                             Acoustics.Label(target.Class) + " contact, quality " + target.Quality.ToString("F2"));
            return t;
        }

        internal static Decoy LaunchDecoy(Unit parent, DecoyKind kind)
        {
            if (!Net.IsServer || parent == null)
                return null;
            Vector3 pos = Geo.GlobalOf(parent) - parent.transform.forward * 40f;
            Vector3 vel = Vector3.zero;
            Ship s = parent as Ship;
            if (s != null && s.rb != null)
                vel = s.rb.velocity;
            Decoy d = new Decoy(kind, parent, pos, vel);
            Decoys.Add(d);
            Sync.SubNet.SendDecoySpawn(d, null);
            if (SubConfig.VerboseLog.Value)
                SubmarinesPlugin.Log.LogInfo(parent.unitName + " launched a " + kind + " decoy.");
            return d;
        }

        internal static void BroadcastPing(Unit pinger, Vector3 pingerGlobal)
        {
            for (int i = 0; i < Arrays.Count; i++)
            {
                if (Arrays[i] != null)
                    Arrays[i].HearPing(pinger, pingerGlobal);
            }
        }

        /// <summary>A short loud noise (launch, blowing ballast) that gives away the source's position.</summary>
        internal static void Transient(Unit source, float range)
        {
            if (source == null)
                return;
            Vector3 p = Geo.GlobalOf(source);
            for (int i = 0; i < Arrays.Count; i++)
            {
                SonarArray a = Arrays[i];
                if (a == null || a.Owner == null || !Net.Hostile(a.Owner.NetworkHQ, source.NetworkHQ))
                    continue;
                if ((Geo.GlobalOf(a.Owner) - p).magnitude > range)
                    continue;
                a.HearPing(source, p);
            }
        }

        internal static bool AnyHostileSubWithin(Unit from, float range)
        {
            Vector3 p = Geo.GlobalOf(from);
            for (int i = 0; i < Subs.Count; i++)
            {
                Submarine s = Subs[i];
                if (s == null || s.Ship == null || s.Ship.disabled || !Net.Hostile(from.NetworkHQ, s.Ship.NetworkHQ))
                    continue;
                if ((Geo.GlobalOf(s.Ship) - p).magnitude < range)
                    return true;
            }
            return false;
        }
    }
}
