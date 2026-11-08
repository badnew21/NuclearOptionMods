using UnityEngine;

namespace Submarines.Runtime
{
    internal enum DecoyKind
    {
        /// <summary>Loud broadband noise source. Masks the boat and seduces torpedo seekers.</summary>
        Noisemaker,

        /// <summary>
        /// Acoustic repeater. Replays the boat's own signature (and answers active pings) from a
        /// phantom position that it walks away from the boat: a false contact with a convincing track.
        /// </summary>
        Repeater,
    }

    /// <summary>A sonar countermeasure in the water. Server-side simulation, stored in global space.</summary>
    internal sealed class Decoy
    {
        private static int _nextId = 1;

        internal readonly int Id;
        internal readonly DecoyKind Kind;
        internal readonly FactionHQ Faction;
        internal readonly Unit Parent;
        internal readonly float Born;
        internal readonly float Expires;
        internal Vector3 Pos;
        internal Vector3 Drift;
        internal bool Dead;

        // Repeater phantom
        internal Vector3 EchoPos;
        private readonly Vector3 _echoDir;
        private float _echoRange;
        private readonly float _parentSl;
        private readonly float _parentTs;

        internal Vector3 EchoDir { get { return _echoDir; } }
        internal float LifeLeft { get { return Expires - Time.time; } }

        /// <summary>Client mirror of a server decoy. Its motion is deterministic, so a spawn message is enough.</summary>
        internal Decoy(int id, DecoyKind kind, Unit parent, Vector3 globalPos, Vector3 drift, Vector3 echoDir, float lifeLeft, float echoRange)
        {
            Id = id;
            Kind = kind;
            Parent = parent;
            Faction = parent != null ? parent.NetworkHQ : null;
            Born = Time.time;
            Expires = Born + lifeLeft;
            Pos = globalPos;
            Drift = drift;
            _echoDir = echoDir;
            _echoRange = echoRange;
            EchoPos = globalPos + echoDir * echoRange;
            _parentSl = parent != null ? Acoustics.SourceLevel(parent) : 110f;
            _parentTs = parent != null ? Acoustics.TargetStrength(parent) : 12f;
        }

        internal float EchoRange { get { return _echoRange; } }

        internal Decoy(DecoyKind kind, Unit parent, Vector3 globalPos, Vector3 parentVelocity)
        {
            Id = _nextId++;
            Kind = kind;
            Parent = parent;
            Faction = parent != null ? parent.NetworkHQ : null;
            Born = Time.time;
            Expires = Born + SubConfig.DecoyLifetime.Value;
            Pos = globalPos;
            // Noisemakers hover near launch depth; repeaters are self-propelled at a few knots.
            Drift = kind == DecoyKind.Repeater ? parentVelocity * 0.4f : Vector3.zero;

            Vector2 rnd = Random.insideUnitCircle.normalized;
            if (rnd.sqrMagnitude < 0.5f)
                rnd = Vector2.right;
            _echoDir = new Vector3(rnd.x, 0f, rnd.y);
            _echoRange = 250f;
            EchoPos = globalPos + _echoDir * _echoRange;
            _parentSl = parent != null ? Acoustics.SourceLevel(parent) : 110f;
            _parentTs = parent != null ? Acoustics.TargetStrength(parent) : 12f;
        }

        internal bool Alive { get { return !Dead && Time.time < Expires; } }

        internal float Age { get { return Time.time - Born; } }

        /// <summary>Loudness heard by passive arrays (at <see cref="NoisePos"/>).</summary>
        internal float SourceLevel
        {
            get { return Kind == DecoyKind.Noisemaker ? SubConfig.DecoySourceLevel.Value : _parentSl + 6f; }
        }

        /// <summary>Where the decoy's noise appears to come from.</summary>
        internal Vector3 NoisePos { get { return Kind == DecoyKind.Repeater ? EchoPos : Pos; } }

        /// <summary>Echo strength answering active pings, at <see cref="NoisePos"/>.</summary>
        internal float EchoStrength { get { return Kind == DecoyKind.Repeater ? _parentTs + 6f : -5f; } }

        internal void Tick(float dt)
        {
            Pos += Drift * dt;
            if (Kind != DecoyKind.Repeater)
                return;

            // Range-gate pull-off: the phantom slides away from the boat at ~35 kn.
            _echoRange = Mathf.Min(_echoRange + 18f * dt, SubConfig.FalseEchoOffset.Value);
            Vector3 anchor = Parent != null && !Parent.disabled ? Geo.GlobalOf(Parent) : Pos;
            EchoPos = anchor + _echoDir * _echoRange;
        }
    }

    internal struct PingEvent
    {
        internal Vector3 Pos;
        internal Unit Pinger;
        internal FactionHQ Faction;
        internal float Time;
    }
}
