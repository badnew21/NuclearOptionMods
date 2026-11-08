using UnityEngine;

namespace Submarines.Runtime
{
    /// <summary>
    /// One track held by a sonar array. A contact may be real (Unit set) or false (Decoy set):
    /// the array itself cannot tell the difference, which is the whole point of the spoofing suite.
    /// </summary>
    internal sealed class SonarContact
    {
        internal long Key;
        internal Unit Unit;
        internal Decoy Decoy;
        internal float FirstSeen;
        internal float LastSeen;
        internal float LastReported;
        internal float Excess;
        internal float Bearing;
        internal float TrueRange;
        internal ContactClass Class;
        internal float ActiveUntil;
        internal Vector3 EstimatedPos;

        // Fixed per-track error draws so the estimate converges smoothly instead of jittering.
        internal float RangeErrDraw;
        internal float BearingErrDraw;
        private float _qualityBoost;

        /// <summary>Held on an active return in the last 30 s (good range).</summary>
        internal bool ActiveReturn { get { return Time.time < ActiveUntil; } }

        /// <summary>True if this track is a decoy or phantom. Only used by the debug display.</summary>
        internal bool IsFalse { get { return Unit == null; } }

        /// <summary>0..1 firing-solution quality. Passive tracks need time (target motion analysis).</summary>
        internal float Quality
        {
            get
            {
                float tracked = LastSeen - FirstSeen;
                float q = 1f - Mathf.Exp(-tracked / 40f);
                if (ActiveReturn)
                    q = Mathf.Max(q, 0.9f);
                return Mathf.Clamp01(Mathf.Max(q, _qualityBoost));
            }
        }

        internal void Boost(float q)
        {
            _qualityBoost = Mathf.Max(_qualityBoost, q);
        }

        internal bool Stale(float now, float maxAge) { return now - LastSeen > maxAge; }

        internal void UpdateEstimate(Vector3 observerGlobal, Vector3 trueGlobal)
        {
            float q = Quality;
            Vector3 d = trueGlobal - observerGlobal;
            float r = d.magnitude;
            TrueRange = r;
            Bearing = Geo.Bearing(observerGlobal, trueGlobal);
            if (r < 1f)
            {
                EstimatedPos = trueGlobal;
                return;
            }
            // Passive: bearing is good, range is poor until the track matures. Active: both good.
            float rangeErr = (ActiveReturn ? 0.03f : 0.35f) * (1f - q) * RangeErrDraw;
            float bearingErr = (ActiveReturn ? 0.5f : 4f) * (1f - q) * BearingErrDraw;
            Vector3 dir = Quaternion.Euler(0f, bearingErr, 0f) * (d / r);
            EstimatedPos = observerGlobal + dir * (r * (1f + rangeErr));
            EstimatedPos.y = trueGlobal.y;
        }
    }
}
