namespace Submarines
{
    internal enum SubRole
    {
        Attack,
        Ballistic,
    }

    /// <summary>Static description of a submarine class.</summary>
    internal sealed class SubSpec
    {
        internal SubRole Role;
        internal string JsonKey;
        internal string UnitName;
        internal string Code;
        internal string Description;

        /// <summary>Radiated noise at 5 kn (dB re 1 uPa @ 1 m). Lower = quieter.</summary>
        internal float QuietSourceLevel;

        /// <summary>Extra dB per doubling of speed above 5 kn.</summary>
        internal float SpeedNoiseSlope;

        /// <summary>Passive sonar detection threshold. Lower = better ears.</summary>
        internal float DetectionThreshold;

        /// <summary>Self-noise the boat's own array has to hear through.</summary>
        internal float SelfNoise;

        internal static readonly SubSpec Attack = new SubSpec
        {
            Role = SubRole.Attack,
            JsonKey = "SUB_SSN_Kestrel",
            UnitName = "Kestrel-class SSN",
            Code = "SSN",
            Description =
                "Nuclear attack submarine. Hunts surface groups and other submarines with wire-guided " +
                "heavyweight torpedoes. Invisible to surface radar while submerged; carries noisemaker " +
                "and repeater decoys plus an acoustic mimicry mode to spoof enemy sonar.",
            QuietSourceLevel = 108f,
            SpeedNoiseSlope = 9f,
            DetectionThreshold = 8f,
            SelfNoise = 52f,
        };

        internal static readonly SubSpec Ballistic = new SubSpec
        {
            Role = SubRole.Ballistic,
            JsonKey = "SUB_SSBN_Leviathan",
            UnitName = "Leviathan-class SSBN",
            Code = "SSBN",
            Description =
                "Ballistic missile submarine. Patrols deep and slow, climbs to launch depth and fires " +
                "submarine-launched ballistic missiles at targets known to its faction. Very quiet, " +
                "lightly armed for self defence (6 torpedoes) and carries the same sonar spoofing suite.",
            QuietSourceLevel = 102f,
            SpeedNoiseSlope = 10f,
            DetectionThreshold = 10f,
            SelfNoise = 54f,
        };

        internal static SubSpec ForKey(string jsonKey)
        {
            if (jsonKey == null)
                return null;
            if (jsonKey == Attack.JsonKey)
                return Attack;
            if (jsonKey == Ballistic.JsonKey)
                return Ballistic;
            return null;
        }
    }
}
