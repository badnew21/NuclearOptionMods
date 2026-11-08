using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>
    /// Hull / towed sonar on a ship or submarine. Runs on every peer so the sonar display works on
    /// clients too; only the server reports tracks to the faction HQ and fires weapons from them.
    /// </summary>
    internal sealed class SonarArray : MonoBehaviour
    {
        private const float ScanInterval = 2f;
        private const float ContactTimeout = 30f;
        private const float ActiveRange = 20000f;
        private const float CounterDetectRange = 55000f;
        private const long DecoyKeyBase = 1L << 40;
        private const long TorpedoKeyBase = 2L << 40;

        internal Unit Owner;
        internal Submarine Sub;
        internal float SelfNoiseBase;
        internal float Threshold;
        internal int AswTorpedoes;
        internal float LastPingHeard = -999f;
        internal Vector3 LastPingHeardFrom;

        private readonly Dictionary<long, SonarContact> _contacts = new Dictionary<long, SonarContact>();
        private readonly List<long> _drop = new List<long>();
        private float _nextScan;
        private float _nextAsw;
        private float _nextAutoPing;
        private bool _pingRequested;

        internal ICollection<SonarContact> Contacts { get { return _contacts.Values; } }

        internal static SonarArray Attach(Unit owner, Submarine sub)
        {
            SonarArray a = owner.GetComponent<SonarArray>();
            if (a == null)
                a = owner.gameObject.AddComponent<SonarArray>();
            a.Owner = owner;
            a.Sub = sub;
            if (sub != null)
            {
                a.SelfNoiseBase = sub.Spec.SelfNoise;
                a.Threshold = sub.Spec.DetectionThreshold;
            }
            else
            {
                a.SelfNoiseBase = 58f;   // hull sonar on a noisy surface combatant
                a.Threshold = 10f;
                a.AswTorpedoes = SubConfig.SurfaceAsw.Value ? SubConfig.SurfaceAswTorpedoes.Value : 0;
            }
            a._nextScan = Time.time + Random.Range(0f, ScanInterval);
            a._nextAutoPing = Time.time + Random.Range(10f, 30f);
            UnderwaterWorld.Register(a);
            return a;
        }

        private void OnDestroy()
        {
            UnderwaterWorld.Unregister(this);
        }

        internal SonarContact Get(long key)
        {
            SonarContact c;
            return _contacts.TryGetValue(key, out c) ? c : null;
        }

        internal void RequestPing()
        {
            _pingRequested = true;
        }

        /// <summary>Own noise grows with speed: flow noise over the dome.</summary>
        private float SelfNoise()
        {
            float kn = Acoustics.Knots(Acoustics.SpeedOf(Owner));
            return SelfNoiseBase + Mathf.Max(0f, kn - 8f) * 0.8f;
        }

        private void Update()
        {
            if (Owner == null || Owner.disabled)
                return;
            float now = Time.time;
            if (now < _nextScan)
                return;
            _nextScan = now + ScanInterval;

            if (Sub == null && AswTorpedoes > 0 && now >= _nextAutoPing && Net.IsServer)
            {
                _nextAutoPing = now + 20f;
                if (UnderwaterWorld.AnyHostileSubWithin(Owner, 30000f))
                    _pingRequested = true;
            }

            Scan(now);

            if (Net.IsServer)
            {
                Report(now);
                if (Sub == null)
                    SurfaceAsw(now);
            }
        }

        private void Scan(float now)
        {
            Vector3 me = Geo.GlobalOf(Owner);
            float myDepth = Mathf.Max(Geo.DepthOf(Owner), Sub == null ? 6f : 0f);
            float nl = SelfNoise();
            FactionHQ myHq = Owner.NetworkHQ;
            bool ping = _pingRequested;
            _pingRequested = false;

            if (ping)
                UnderwaterWorld.BroadcastPing(Owner, me);

            List<Decoy> decoys = UnderwaterWorld.Decoys;

            // Real hulls
            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u == Owner || u.disabled || !(u is Ship))
                    continue;
                if (myHq != null && u.NetworkHQ == myHq)
                    continue;
                Vector3 p = Geo.GlobalOf(u);
                float r = (p - me).magnitude;
                if (r > Acoustics.MaxPassiveRange)
                    continue;

                Submarine otherSub = u.GetComponent<Submarine>();
                float tgtDepth = Geo.DepthOf(u);
                float sl = Acoustics.SourceLevel(u);
                float passive = Acoustics.PassiveExcess(sl, r, tgtDepth, myDepth, nl, Threshold);
                if (passive > 0f && otherSub != null && Masked(u, p, me, sl, r, tgtDepth, myDepth, decoys))
                    passive = -1f;

                float active = -1f;
                if (ping && r < ActiveRange)
                    active = Acoustics.ActiveExcess(Acoustics.TargetStrength(u), r, tgtDepth, myDepth, nl, Threshold);

                if (passive <= 0f && active <= 0f)
                    continue;

                ContactClass cls;
                if (otherSub == null)
                    cls = ContactClass.Surface;
                else if (active <= 0f && otherSub.MimicActive)
                    cls = ContactClass.Merchant;
                else
                    cls = ContactClass.Submarine;

                SonarContact c = Touch(u.GetInstanceID(), u, null, now, Mathf.Max(passive, active), cls);
                if (active > 0f)
                    c.ActiveUntil = now + 30f;
                c.UpdateEstimate(me, p);
            }

            // Decoys and phantoms
            for (int i = 0; i < decoys.Count; i++)
            {
                Decoy d = decoys[i];
                if (!d.Alive || (myHq != null && d.Faction == myHq))
                    continue;
                Vector3 p = d.NoisePos;
                float r = (p - me).magnitude;
                float dDepth = Geo.DepthGlobal(p);
                float passive = Acoustics.PassiveExcess(d.SourceLevel, r, dDepth, myDepth, nl, Threshold);
                float active = ping && r < ActiveRange
                    ? Acoustics.ActiveExcess(d.EchoStrength, r, dDepth, myDepth, nl, Threshold)
                    : -1f;
                if (passive <= 0f && active <= 0f)
                    continue;

                long key = DecoyKeyBase + d.Id;
                SonarContact existing = Get(key);
                ContactClass cls = ContactClass.Submarine;
                // A noisemaker sits still in the water. Track it long enough and it gives itself away.
                if (d.Kind == DecoyKind.Noisemaker && existing != null && existing.Quality > 0.6f &&
                    now - existing.FirstSeen > 25f)
                    cls = ContactClass.Decoy;
                SonarContact c = Touch(key, null, d, now, Mathf.Max(passive, active), cls);
                if (active > 0f)
                    c.ActiveUntil = now + 30f;
                c.UpdateEstimate(me, p);
            }

            // Torpedoes are loud (~150 dB) and easy to hear.
            List<Torpedo> torps = UnderwaterWorld.Torpedoes;
            for (int i = 0; i < torps.Count; i++)
            {
                Torpedo t = torps[i];
                if (t.Dead || (myHq != null && t.Faction == myHq))
                    continue;
                float r = (t.Pos - me).magnitude;
                float e = Acoustics.PassiveExcess(150f, r, Geo.DepthGlobal(t.Pos), myDepth, nl, Threshold);
                if (e <= 0f)
                    continue;
                SonarContact c = Touch(TorpedoKeyBase + t.Id, null, null, now, e, ContactClass.Torpedo);
                c.Boost(0.8f);
                c.UpdateEstimate(me, t.Pos);
            }

            _drop.Clear();
            foreach (KeyValuePair<long, SonarContact> kv in _contacts)
            {
                if (kv.Value.Stale(now, ContactTimeout) ||
                    (kv.Value.Decoy != null && !kv.Value.Decoy.Alive) ||
                    (kv.Value.Unit != null && kv.Value.Unit.disabled))
                    _drop.Add(kv.Key);
            }
            for (int i = 0; i < _drop.Count; i++)
                _contacts.Remove(_drop[i]);
        }

        /// <summary>
        /// A loud noisemaker on nearly the same bearing drowns out the quieter boat behind it.
        /// </summary>
        private static bool Masked(Unit sub, Vector3 subPos, Vector3 me, float subSl, float r, float subDepth,
                                   float myDepth, List<Decoy> decoys)
        {
            float subLevel = subSl - Acoustics.TransmissionLoss(r, subDepth, myDepth);
            float subBearing = Geo.Bearing(me, subPos);
            for (int i = 0; i < decoys.Count; i++)
            {
                Decoy d = decoys[i];
                if (!d.Alive || d.Kind != DecoyKind.Noisemaker || d.Faction != sub.NetworkHQ)
                    continue;
                float db = Mathf.Abs(Mathf.DeltaAngle(subBearing, Geo.Bearing(me, d.Pos)));
                if (db > 12f)
                    continue;
                float dr = (d.Pos - me).magnitude;
                float decoyLevel = d.SourceLevel - Acoustics.TransmissionLoss(dr, Geo.DepthGlobal(d.Pos), myDepth);
                if (decoyLevel > subLevel + 3f)
                    return true;
            }
            return false;
        }

        private SonarContact Touch(long key, Unit unit, Decoy decoy, float now, float excess, ContactClass cls)
        {
            SonarContact c;
            if (!_contacts.TryGetValue(key, out c))
            {
                c = new SonarContact
                {
                    Key = key,
                    Unit = unit,
                    Decoy = decoy,
                    FirstSeen = now,
                    RangeErrDraw = Random.Range(-1f, 1f),
                    BearingErrDraw = Random.Range(-1f, 1f),
                };
                _contacts[key] = c;
            }
            c.LastSeen = now;
            c.Excess = excess;
            c.Class = cls;
            return c;
        }

        /// <summary>Someone pinged: a passive array hears the ping much farther than the pinger hears echoes.</summary>
        internal void HearPing(Unit pinger, Vector3 pingerGlobal)
        {
            if (Owner == null || pinger == null || pinger == Owner || !Net.Hostile(Owner.NetworkHQ, pinger.NetworkHQ))
                return;
            Vector3 me = Geo.GlobalOf(Owner);
            if ((pingerGlobal - me).magnitude > CounterDetectRange)
                return;
            float now = Time.time;
            LastPingHeard = now;
            LastPingHeardFrom = pingerGlobal;
            ContactClass cls = pinger.GetComponent<Submarine>() != null ? ContactClass.Submarine : ContactClass.Surface;
            SonarContact c = Touch(pinger.GetInstanceID(), pinger, null, now, 30f, cls);
            c.Boost(0.6f);
            c.UpdateEstimate(me, pingerGlobal);
        }

        private void Report(float now)
        {
            FactionHQ hq = Owner.NetworkHQ;
            if (hq == null)
                return;
            foreach (SonarContact c in _contacts.Values)
            {
                if (c.Unit == null || c.Unit.disabled || !Net.Hostile(hq, c.Unit.NetworkHQ))
                    continue;
                if (c.Class != ContactClass.Submarine && c.Class != ContactClass.Surface)
                    continue;
                if (c.Quality < 0.35f || now - c.LastReported < 4f || now - c.LastSeen > ScanInterval + 0.5f)
                    continue;
                c.LastReported = now;
                try { hq.RpcUpdateTrackingInfo(c.Unit.persistentID); }
                catch (System.Exception ex)
                {
                    if (SubConfig.VerboseLog.Value)
                        SubmarinesPlugin.Log.LogWarning("RpcUpdateTrackingInfo failed: " + ex.Message);
                }
            }
        }

        /// <summary>Best hostile track of the wanted kind, for weapon employment.</summary>
        internal SonarContact BestTarget(float maxRange, float minQuality, bool subs, bool surface)
        {
            SonarContact best = null;
            float bestScore = float.MinValue;
            Vector3 me = Geo.GlobalOf(Owner);
            foreach (SonarContact c in _contacts.Values)
            {
                bool wanted = (subs && c.Class == ContactClass.Submarine) || (surface && c.Class == ContactClass.Surface);
                if (!wanted || c.Quality < minQuality)
                    continue;
                if (c.Unit != null && !Net.Hostile(Owner.NetworkHQ, c.Unit.NetworkHQ))
                    continue;
                if (c.Decoy != null && !Net.Hostile(Owner.NetworkHQ, c.Decoy.Faction))
                    continue;
                float r = (c.EstimatedPos - me).magnitude;
                if (r > maxRange)
                    continue;
                float score = c.Quality * 2f - r / maxRange + c.Excess * 0.02f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>Nearest torpedo track, or null.</summary>
        internal SonarContact NearestTorpedo(out float range)
        {
            range = float.MaxValue;
            SonarContact best = null;
            Vector3 me = Geo.GlobalOf(Owner);
            foreach (SonarContact c in _contacts.Values)
            {
                if (c.Class != ContactClass.Torpedo)
                    continue;
                float r = (c.EstimatedPos - me).magnitude;
                if (r < range)
                {
                    range = r;
                    best = c;
                }
            }
            return best;
        }

        private void SurfaceAsw(float now)
        {
            if (AswTorpedoes <= 0 || now < _nextAsw)
                return;
            SonarContact c = BestTarget(SubConfig.SurfaceAswRange.Value, 0.5f, true, false);
            if (c == null)
                return;
            _nextAsw = now + 60f;
            AswTorpedoes--;
            UnderwaterWorld.LaunchTorpedo(Owner, this, c, true);
        }
    }
}
