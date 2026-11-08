using UnityEngine;

namespace Submarines.Runtime
{
    internal enum ContactClass
    {
        Unknown,
        Submarine,
        Surface,
        Merchant,   // what a mimicking sub sounds like
        Torpedo,
        Decoy,      // a noisemaker that has been tracked long enough to be recognised
    }

    /// <summary>
    /// Simplified sonar equation, all in dB:
    ///   passive SE = SL - TL - NL + DI - DT
    ///   active  SE = SLping - 2TL + TS - NL + DI - DT
    /// TL is spherical spreading plus absorption, plus a penalty when the thermocline sits between
    /// source and receiver.
    /// </summary>
    internal static class Acoustics
    {
        internal const float PingSourceLevel = 220f;
        internal const float AmbientNoise = 50f;
        internal const float ArrayGain = 20f;
        internal const float AbsorptionDbPerKm = 0.5f;
        internal const float MaxPassiveRange = 60000f;

        internal static float TransmissionLoss(float rangeM, float depthA, float depthB)
        {
            float r = Mathf.Max(rangeM, 1f);
            float tl = 20f * Mathf.Log10(r) + AbsorptionDbPerKm * r / 1000f;
            float layer = SubConfig.ThermalLayerDepth.Value;
            if ((depthA > layer) != (depthB > layer))
                tl += SubConfig.ThermalLayerLossDb.Value;
            return tl;
        }

        internal static float Knots(float mps) { return mps * 1.94384f; }

        /// <summary>Radiated noise of any ship-type unit.</summary>
        internal static float SourceLevel(Unit u)
        {
            if (u == null)
                return 0f;
            Submarine sub = u.GetComponent<Submarine>();
            if (sub != null)
                return sub.SourceLevel();

            float len = u.definition != null ? Mathf.Max(u.definition.length, 10f) : 80f;
            float kn = Knots(SpeedOf(u));
            // Bigger and faster is louder. A 150 m destroyer at 20 kn comes out around 145 dB.
            return 128f + 10f * Mathf.Log10(len / 50f) + 8f * Mathf.Log(Mathf.Max(kn, 5f) / 5f, 2f) + 3f;
        }

        internal static float SpeedOf(Unit u)
        {
            Ship s = u as Ship;
            if (s != null && s.rb != null)
                return s.rb.velocity.magnitude;
            Rigidbody rb = u.GetComponent<Rigidbody>();
            return rb != null ? rb.velocity.magnitude : 0f;
        }

        /// <summary>Echo strength. Long hulls reflect more; subs have anechoic coating.</summary>
        internal static float TargetStrength(Unit u)
        {
            float len = u.definition != null ? Mathf.Max(u.definition.length, 10f) : 80f;
            float ts = 10f * Mathf.Log10(len / 10f) + 12f;
            if (u.GetComponent<Submarine>() != null)
                ts -= 8f;
            return ts;
        }

        internal static float PassiveExcess(float sl, float range, float depthSrc, float depthRcv, float selfNoise, float dt)
        {
            float nl = Mathf.Max(AmbientNoise, selfNoise);
            return sl - TransmissionLoss(range, depthSrc, depthRcv) - nl + ArrayGain - dt;
        }

        internal static float ActiveExcess(float ts, float range, float depthTgt, float depthRcv, float selfNoise, float dt)
        {
            float nl = Mathf.Max(AmbientNoise, selfNoise);
            float tl = TransmissionLoss(range, depthTgt, depthRcv);
            return PingSourceLevel - 2f * tl + ts - nl + ArrayGain - dt;
        }

        internal static string Label(ContactClass c)
        {
            switch (c)
            {
                case ContactClass.Submarine: return "SUB";
                case ContactClass.Surface: return "SURF";
                case ContactClass.Merchant: return "MERCH";
                case ContactClass.Torpedo: return "TORP";
                case ContactClass.Decoy: return "DECOY";
                default: return "UNK";
            }
        }
    }
}
