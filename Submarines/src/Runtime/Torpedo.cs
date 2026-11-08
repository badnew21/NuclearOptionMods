using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>
    /// Scripted heavyweight torpedo (server only). Not a game Unit: it never shows on radar and the
    /// only way to know it is coming is sonar.
    ///
    /// Run: wire-guided toward the launcher's contact estimate, seeker enables 3 km out (or when the
    /// wire is cut), then homes on the loudest source in its 40 degree cone. That last part is what
    /// the decoys exploit: a noisemaker is 25-30 dB louder than a quiet boat.
    /// </summary>
    internal sealed class Torpedo
    {
        private static int _nextId = 1;

        private const float SeekerRange = 2600f;
        private const float SeekerHalfCone = 40f;
        private const float TurnRateDeg = 14f;

        internal readonly int Id;
        internal readonly Unit Owner;
        internal readonly FactionHQ Faction;
        internal readonly PersistentID OwnerId;
        internal readonly bool Asw;
        internal Vector3 Pos;
        internal Vector3 Heading;
        internal bool Dead;

        private readonly float _speed;
        private float _runLeft;
        private readonly SonarArray _wireArray;
        private readonly long _wireContact;
        private Vector3 _aim;
        private float _nextSeek;
        private float _nextBottomCheck;

        // Seeker lock: exactly one of these is set.
        internal Unit LockUnit;
        internal Decoy LockDecoy;
        private bool _lockIsEcho;
        private readonly HashSet<int> _rejectedEchoes = new HashSet<int>();

        /// <summary>Client-side mirror of a server torpedo: dead-reckoned between SubNet updates.</summary>
        internal readonly bool Remote;
        internal float LastUpdate;

        internal float Speed { get { return _speed; } }

        internal Torpedo(int id, Unit owner, Vector3 pos, Vector3 heading, float speed, bool asw)
        {
            Id = id;
            Remote = true;
            Owner = owner;
            Faction = owner != null ? owner.NetworkHQ : null;
            Asw = asw;
            Pos = pos;
            Heading = heading;
            _speed = speed;
            LastUpdate = Time.time;
        }

        internal void ApplyRemote(Vector3 pos, Vector3 heading)
        {
            // Blend toward the authoritative position rather than snapping.
            Pos = (pos - Pos).sqrMagnitude > 400f ? pos : Vector3.Lerp(Pos, pos, 0.5f);
            Heading = heading;
            LastUpdate = Time.time;
        }

        internal Torpedo(Unit owner, SonarArray wireArray, SonarContact contact, Vector3 aimGlobal, bool asw)
        {
            Id = _nextId++;
            Owner = owner;
            Faction = owner.NetworkHQ;
            OwnerId = owner.persistentID;
            Asw = asw;
            _speed = asw ? SubConfig.TorpedoSpeed.Value * 0.85f : SubConfig.TorpedoSpeed.Value;
            _runLeft = asw ? SubConfig.TorpedoRange.Value * 0.5f : SubConfig.TorpedoRange.Value;
            _wireArray = wireArray;
            _wireContact = contact != null ? contact.Key : 0;
            _aim = aimGlobal;

            Pos = Geo.GlobalOf(owner);
            float startDepth = Mathf.Max(Geo.DepthOf(owner) + 5f, asw ? 20f : 10f);
            Pos.y = Geo.ToGlobal(new Vector3(0f, Geo.Sea - startDepth, 0f)).y;
            Vector3 flat = aimGlobal - Pos;
            flat.y = 0f;
            Heading = flat.sqrMagnitude > 1f ? flat.normalized : owner.transform.forward;
        }

        internal bool WireIntact
        {
            get { return _wireArray != null && Owner != null && !Owner.disabled && _wireContact != 0; }
        }

        internal bool Homing { get { return LockUnit != null || LockDecoy != null; } }

        internal void Tick(float dt)
        {
            if (Dead)
                return;
            if (Remote)
            {
                Pos += Heading * _speed * dt;
                if (Time.time - LastUpdate > 3f)
                    Dead = true;
                return;
            }

            Vector3 goal = CurrentGoal();
            Steer(goal, dt);
            Pos += Heading * _speed * dt;
            _runLeft -= _speed * dt;
            if (_runLeft <= 0f)
            {
                Die("fuel exhausted");
                return;
            }

            if (Time.time >= _nextBottomCheck)
            {
                _nextBottomCheck = Time.time + 1f;
                Vector3 local = Geo.ToLocal(Pos);
                if (Geo.WaterDepthLocal(local) < Geo.DepthLocal(local) + 2f)
                {
                    Die("hit the seabed");
                    return;
                }
            }

            if (Time.time >= _nextSeek)
            {
                _nextSeek = Time.time + 0.5f;
                bool seekerOn = !WireIntact || (Pos - _aim).magnitude < 3000f || Homing;
                if (seekerOn)
                    Seek();
            }

            CheckTerminal();
        }

        private Vector3 CurrentGoal()
        {
            if (LockUnit != null && !LockUnit.disabled)
                return Geo.GlobalOf(LockUnit);
            if (LockDecoy != null && LockDecoy.Alive)
                return _lockIsEcho ? LockDecoy.EchoPos : LockDecoy.Pos;

            LockUnit = null;
            LockDecoy = null;
            if (WireIntact)
            {
                SonarContact c = _wireArray.Get(_wireContact);
                if (c != null)
                    _aim = c.EstimatedPos;
            }
            return _aim;
        }

        private void Steer(Vector3 goal, float dt)
        {
            Vector3 to = goal - Pos;
            // Run deep under surface targets, match depth on submarines.
            float goalDepth = Geo.DepthGlobal(goal);
            float wantDepth = Mathf.Clamp(goalDepth < 3f ? 9f : goalDepth, 6f, SubConfig.MaxDepth.Value + 50f);
            float curDepth = Geo.DepthGlobal(Pos);
            float vy = Mathf.Clamp((curDepth - wantDepth) * 0.3f, -6f, 6f);

            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 h = new Vector3(Heading.x, 0f, Heading.z);
            if (flat.sqrMagnitude > 1f && h.sqrMagnitude > 0.0001f)
                h = Vector3.RotateTowards(h.normalized, flat.normalized, TurnRateDeg * Mathf.Deg2Rad * dt, 0f);
            h = h.normalized * Mathf.Sqrt(Mathf.Max(0f, 1f - (vy / _speed) * (vy / _speed)));
            Heading = new Vector3(h.x, vy / _speed, h.z).normalized;
        }

        private void Seek()
        {
            float bestLevel = float.MinValue;
            Unit bestUnit = null;
            Decoy bestDecoy = null;
            bool bestEcho = false;
            float myDepth = Geo.DepthGlobal(Pos);

            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || !(u is Ship) || !Net.Hostile(Faction, u.NetworkHQ))
                    continue;
                Vector3 p = Geo.GlobalOf(u);
                float lvl;
                if (!InCone(p, out lvl, Acoustics.SourceLevel(u), myDepth, Geo.DepthOf(u)))
                    continue;
                if (lvl > bestLevel)
                {
                    bestLevel = lvl;
                    bestUnit = u;
                    bestDecoy = null;
                }
            }

            List<Decoy> decoys = UnderwaterWorld.Decoys;
            for (int i = 0; i < decoys.Count; i++)
            {
                Decoy d = decoys[i];
                if (!d.Alive || !Net.Hostile(Faction, d.Faction))
                    continue;
                bool echo = d.Kind == DecoyKind.Repeater;
                if (echo && _rejectedEchoes.Contains(d.Id))
                    continue;
                float lvl;
                if (!InCone(d.NoisePos, out lvl, d.SourceLevel, myDepth, Geo.DepthGlobal(d.NoisePos)))
                    continue;
                if (lvl > bestLevel)
                {
                    bestLevel = lvl;
                    bestUnit = null;
                    bestDecoy = d;
                    bestEcho = echo;
                }
            }

            if (bestUnit == null && bestDecoy == null)
                return;

            if ((bestUnit != null && bestUnit != LockUnit) || (bestDecoy != null && bestDecoy != LockDecoy))
            {
                if (SubConfig.VerboseLog.Value)
                    SubmarinesPlugin.Log.LogInfo("Torpedo " + Id + " acquired " +
                                                 (bestUnit != null ? bestUnit.unitName : (bestEcho ? "a false echo" : "a noisemaker")));
            }
            LockUnit = bestUnit;
            LockDecoy = bestDecoy;
            _lockIsEcho = bestEcho;
        }

        private bool InCone(Vector3 p, out float level, float sl, float myDepth, float tgtDepth)
        {
            level = 0f;
            Vector3 to = p - Pos;
            float r = to.magnitude;
            if (r > SeekerRange || r < 1f)
                return false;
            if (Vector3.Angle(new Vector3(Heading.x, 0f, Heading.z), new Vector3(to.x, 0f, to.z)) > SeekerHalfCone)
                return false;
            // Seeker self-noise at 50 kn is high; it only hears what is close and loud.
            level = Acoustics.PassiveExcess(sl, r, tgtDepth, myDepth, 70f, 0f);
            return level > 0f;
        }

        private void CheckTerminal()
        {
            if (LockUnit != null && !LockUnit.disabled)
            {
                Vector3 p = Geo.GlobalOf(LockUnit);
                float hull = LockUnit.definition != null ? Mathf.Max(LockUnit.definition.length * 0.45f, 12f) : 15f;
                float flat = Geo.FlatDistance(p, Pos);
                float dy = Mathf.Abs(Geo.DepthGlobal(Pos) - Mathf.Max(Geo.DepthOf(LockUnit), 4f));
                if (flat < hull && dy < 25f)
                {
                    Warhead.Detonate(Geo.ToLocal(Pos), LockUnit, Owner, OwnerId, SubConfig.TorpedoDamage.Value);
                    Die("hit " + LockUnit.unitName);
                }
                return;
            }

            if (LockDecoy != null && LockDecoy.Alive)
            {
                if (_lockIsEcho)
                {
                    // Arrived at a phantom: nothing there. Re-attack, ignoring that repeater.
                    if ((LockDecoy.EchoPos - Pos).magnitude < 60f)
                    {
                        _rejectedEchoes.Add(LockDecoy.Id);
                        LockDecoy = null;
                        if (SubConfig.VerboseLog.Value)
                            SubmarinesPlugin.Log.LogInfo("Torpedo " + Id + " overran a false echo, re-attacking.");
                    }
                }
                else if ((LockDecoy.Pos - Pos).magnitude < 25f)
                {
                    LockDecoy.Dead = true;
                    Die("seduced by a noisemaker");
                }
            }
        }

        internal void Die(string why)
        {
            if (Dead)
                return;
            Dead = true;
            if (SubConfig.VerboseLog.Value)
                SubmarinesPlugin.Log.LogInfo("Torpedo " + Id + " ended: " + why);
        }
    }
}
