using NuclearOption.Networking;
using Submarines.Bootstrap;
using UnityEngine;

namespace Submarines.Runtime
{
    internal enum SubMode
    {
        Patrol,
        Attack,
        Launch,
        Evade,
        Manual,
    }

    /// <summary>
    /// Turns a stock ship hull into a submarine: depth keeping, AI tactics, weapons and the
    /// countermeasure suite. Physics and decisions run on the server only; clients just see the
    /// synced transform.
    /// </summary>
    internal sealed class Submarine : MonoBehaviour
    {
        internal Ship Ship;
        internal SubSpec Spec;
        internal SonarArray Sonar;
        internal SubMode Mode = SubMode.Patrol;

        internal float OrderedDepth;
        internal int Torpedoes;
        internal int Missiles;
        internal int DecoysLeft;

        /// <summary>Player asked for an SLBM launch; fire once at launch depth.</summary>
        internal bool PendingLaunch;

        private float _manualDepth = -1f;
        private float _nextBrain;
        private float _nextTorpedo;
        private float _nextMissile;
        private float _nextDecoy;
        private float _evadeUntil;
        private float _mimicUntil;
        private float _nextMimic;
        private float _waterDepth = 5000f;
        private float _nextBottomProbe;
        private bool _decoyToggle;

        // Depth-keeping disturbance observer (estimates buoyancy + waves so we can cancel them).
        private float _disturbance;
        private float _lastCmdVy;
        private bool _haveLast;

        // Client-side copy of server state (SubNet).
        internal bool HasSync;
        internal float SyncedDepth;

        internal float MimicLeft { get { return Mathf.Max(0f, _mimicUntil - Time.time); } }

        internal void ApplyState(float depth, float ordered, SubMode mode, int torps, int missiles, int decoys,
                                 float mimicLeft, float mimicCooldown, bool pending)
        {
            HasSync = true;
            SyncedDepth = depth;
            OrderedDepth = ordered;
            Mode = mode;
            Torpedoes = torps;
            Missiles = missiles;
            DecoysLeft = decoys;
            _mimicUntil = Time.time + mimicLeft;
            _nextMimic = Time.time + mimicCooldown;
            PendingLaunch = pending;
        }

        internal static Submarine Attach(Ship ship, SubSpec spec)
        {
            Submarine s = ship.GetComponent<Submarine>();
            if (s != null)
                return s;
            s = ship.gameObject.AddComponent<Submarine>();
            s.Ship = ship;
            s.Spec = spec;
            s.Torpedoes = spec.Role == SubRole.Attack ? SubConfig.AttackTorpedoes.Value : 6;
            s.Missiles = spec.Role == SubRole.Ballistic ? SubConfig.BallisticMissiles.Value : 0;
            s.DecoysLeft = SubConfig.Decoys.Value;
            s.OrderedDepth = SubConfig.PatrolDepth.Value;
            s._nextMissile = Time.time + 30f;
            s.Sonar = SonarArray.Attach(ship, s);
            Visual.SubVisual.Apply(ship.gameObject, spec);
            UnderwaterWorld.Register(s);
            SubmarinesPlugin.Log.LogInfo(spec.UnitName + " '" + ship.unitName + "' on station.");
            return s;
        }

        private void OnDestroy()
        {
            UnderwaterWorld.Unregister(this);
        }

        internal float Depth { get { return Ship != null ? Geo.DepthOf(Ship) : 0f; } }

        /// <summary>Submerged enough that surface radar cannot see the hull.</summary>
        internal bool Submerged { get { return Depth > SubConfig.SubmergedThreshold.Value; } }

        /// <summary>Masts/periscope still above water: short-range radar and eyes can pick them up.</summary>
        internal bool MastsExposed { get { return Depth <= SubConfig.PeriscopeDepth.Value + 2f; } }

        internal bool MimicActive { get { return Time.time < _mimicUntil; } }
        internal float MimicCooldownLeft { get { return Mathf.Max(0f, _nextMimic - Time.time); } }

        internal float SourceLevel()
        {
            float kn = Acoustics.Knots(Acoustics.SpeedOf(Ship));
            float sl = Spec.QuietSourceLevel + Spec.SpeedNoiseSlope * Mathf.Log(Mathf.Max(kn, 5f) / 5f, 2f);
            // Cavitation near the surface at speed.
            if (Depth < 40f && kn > 12f)
                sl += 6f;
            if (!Submerged)
                sl += 10f;
            return sl;
        }

        // ---------------------------------------------------------------- orders (server)

        internal void OrderDepth(float depth)
        {
            _manualDepth = Mathf.Clamp(depth, 0f, SubConfig.MaxDepth.Value);
            Mode = SubMode.Manual;
        }

        internal void ClearManual()
        {
            _manualDepth = -1f;
            Mode = SubMode.Patrol;
        }

        internal bool FireTorpedo(SonarContact target)
        {
            if (!Net.IsServer || target == null || Torpedoes <= 0 || Time.time < _nextTorpedo)
                return false;
            Torpedoes--;
            _nextTorpedo = Time.time + SubConfig.TorpedoReload.Value;
            UnderwaterWorld.LaunchTorpedo(Ship, Sonar, target, false);
            return true;
        }

        internal bool LaunchDecoy()
        {
            if (!Net.IsServer || DecoysLeft <= 0 || Time.time < _nextDecoy)
                return false;
            DecoysLeft--;
            _nextDecoy = Time.time + SubConfig.DecoyCooldown.Value;
            _decoyToggle = !_decoyToggle;
            UnderwaterWorld.LaunchDecoy(Ship, _decoyToggle ? DecoyKind.Noisemaker : DecoyKind.Repeater);
            return true;
        }

        internal bool EngageMimic()
        {
            if (!Net.IsServer || Time.time < _nextMimic)
                return false;
            _mimicUntil = Time.time + SubConfig.MimicDuration.Value;
            _nextMimic = _mimicUntil + SubConfig.MimicCooldown.Value;
            if (SubConfig.VerboseLog.Value)
                SubmarinesPlugin.Log.LogInfo(Ship.unitName + " engaged acoustic mimicry.");
            return true;
        }

        internal bool LaunchBallistic(Unit target)
        {
            if (!Net.IsServer || Missiles <= 0 || target == null || target.disabled)
                return false;
            Spawner spawner = NetworkSceneSingleton<Spawner>.i;
            MissileDefinition def = SubRegistry.FindMissile(SubConfig.Keys(SubConfig.BallisticMissileKeys));
            if (spawner == null || def == null)
            {
                SubmarinesPlugin.Log.LogWarning("No ballistic missile found for keys '" + SubConfig.BallisticMissileKeys.Value +
                                                "'. Check the missile list in the log.");
                return false;
            }

            // Cold launch: the round is ejected from the tube and lights its motor clear of the water.
            Vector3 pos = Ship.transform.position + Ship.transform.forward * (Ship.definition != null ? Ship.definition.length * 0.1f : 5f);
            pos.y = Geo.Sea + 6f;
            Quaternion rot = Quaternion.LookRotation(Vector3.up, Ship.transform.forward);
            Vector3 vel = Vector3.up * 45f + (Ship.rb != null ? Ship.rb.velocity : Vector3.zero);
            Missile m = null;
            try { m = spawner.SpawnMissile(def, pos, rot, vel, target, Ship); }
            catch (System.Exception ex) { SubmarinesPlugin.Log.LogWarning("SLBM spawn failed: " + ex.Message); }
            if (m == null)
                return false;

            Missiles--;
            _nextMissile = Time.time + SubConfig.BallisticInterval.Value;
            UnderwaterWorld.Transient(Ship, 40000f);
            SubmarinesPlugin.Log.LogInfo(Ship.unitName + " launched " + def.unitName + " at " + target.unitName +
                                         " (" + Missiles + " left).");
            return true;
        }

        // ---------------------------------------------------------------- AI

        private void Update()
        {
            if (Ship == null || Ship.disabled || !Net.IsServer || !Net.InPlay)
                return;

            if (Time.time >= _nextBottomProbe)
            {
                _nextBottomProbe = Time.time + 2f;
                _waterDepth = Geo.WaterDepthLocal(Ship.transform.position);
            }

            if (Time.time < _nextBrain)
                return;
            _nextBrain = Time.time + 1f;
            Think();
        }

        private void Think()
        {
            float now = Time.time;

            // 1. Torpedo in the water and coming our way: countermeasures and a layer jump.
            float torpRange = float.MaxValue;
            SonarContact torp = Sonar != null ? Sonar.NearestTorpedo(out torpRange) : null;
            if (torp != null && torpRange < 6000f)
            {
                if (Mode != SubMode.Evade)
                {
                    float layer = SubConfig.ThermalLayerDepth.Value;
                    OrderedDepth = Depth > layer ? Mathf.Max(SubConfig.PeriscopeDepth.Value + 10f, layer - 30f) : layer + 80f;
                    if (SubConfig.VerboseLog.Value)
                        SubmarinesPlugin.Log.LogInfo(Ship.unitName + " evading torpedo at " + torpRange.ToString("F0") + " m.");
                }
                Mode = SubMode.Evade;
                _evadeUntil = now + 60f;
                LaunchDecoy();
                return;
            }

            // 2. Being pinged by an enemy: we are probably held. Mimic and drop a repeater.
            if (Sonar != null && now - Sonar.LastPingHeard < 4f)
            {
                EngageMimic();
                if (DecoysLeft > SubConfig.Decoys.Value / 2)
                    LaunchDecoy();
            }

            if (Mode == SubMode.Evade && now < _evadeUntil)
                return;
            if (PendingLaunch)
            {
                if (Missiles <= 0)
                {
                    PendingLaunch = false;
                }
                else if (Mathf.Abs(Depth - SubConfig.LaunchDepth.Value) < 6f && Time.time >= _nextMissile)
                {
                    Unit target = FindStrategicTarget();
                    if (target != null)
                        LaunchBallistic(target);
                    PendingLaunch = false;
                }
                else
                {
                    OrderedDepth = SubConfig.LaunchDepth.Value;
                    return;
                }
            }

            if (_manualDepth >= 0f)
            {
                Mode = SubMode.Manual;
                OrderedDepth = _manualDepth;
                return;
            }

            // 3. Weapons.
            if (Spec.Role == SubRole.Attack)
                ThinkAttack();
            else
                ThinkBallistic();
        }

        private void ThinkAttack()
        {
            Mode = SubMode.Patrol;
            OrderedDepth = SubConfig.PatrolDepth.Value;
            if (Torpedoes <= 0 || Sonar == null)
                return;
            SonarContact c = Sonar.BestTarget(SubConfig.TorpedoRange.Value * 0.7f, 0.45f, true, true);
            if (c == null)
                return;
            Mode = SubMode.Attack;
            FireTorpedo(c);
        }

        private void ThinkBallistic()
        {
            // Self defence against hunting subs only.
            if (Torpedoes > 0 && Sonar != null)
            {
                SonarContact c = Sonar.BestTarget(6000f, 0.5f, true, false);
                if (c != null)
                    FireTorpedo(c);
            }

            if (Missiles <= 0 || !SubConfig.BallisticAutoLaunch.Value || Time.time < _nextMissile)
            {
                Mode = SubMode.Patrol;
                OrderedDepth = SubConfig.PatrolDepth.Value;
                return;
            }

            Unit target = FindStrategicTarget();
            if (target == null)
            {
                Mode = SubMode.Patrol;
                OrderedDepth = SubConfig.PatrolDepth.Value;
                return;
            }

            Mode = SubMode.Launch;
            OrderedDepth = SubConfig.LaunchDepth.Value;
            float vy = Ship.rb != null ? Mathf.Abs(Ship.rb.velocity.y) : 0f;
            if (Mathf.Abs(Depth - SubConfig.LaunchDepth.Value) < 6f && vy < 1.5f)
                LaunchBallistic(target);
        }

        /// <summary>Nearest enemy building or ship the faction has a known position for.</summary>
        internal Unit FindStrategicTarget()
        {
            FactionHQ hq = Ship.NetworkHQ;
            if (hq == null)
                return null;
            Vector3 me = Geo.GlobalOf(Ship);
            float maxR = SubConfig.BallisticRange.Value;
            Unit best = null;
            float bestScore = float.MaxValue;
            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || !Net.Hostile(hq, u.NetworkHQ))
                    continue;
                bool building = u.definition is BuildingDefinition;
                bool ship = u is Ship && u.GetComponent<Submarine>() == null;
                if (!building && !ship)
                    continue;
                GlobalPosition known;
                if (!hq.TryGetKnownPosition(u, out known))
                    continue;
                float r = (known.AsVector3() - me).magnitude;
                if (r > maxR || r < 8000f)
                    continue;
                // Prefer high-value fixed targets.
                float score = r / Mathf.Max(u.definition != null ? u.definition.value : 1f, 1f);
                if (building)
                    score *= 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = u;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- depth keeping

        private void FixedUpdate()
        {
            if (Ship == null || Ship.disabled || Ship.rb == null || !Net.InPlay)
                return;

            float want;
            if (Net.IsServer)
            {
                float floor = Mathf.Max(0f, _waterDepth - SubConfig.BottomClearance.Value);
                want = Mathf.Min(Mathf.Clamp(OrderedDepth, 0f, SubConfig.MaxDepth.Value), floor);
            }
            else
            {
                // Client: if this machine simulates the hull at all (non-kinematic body), hold the
                // depth the server reports so local buoyancy can't pop the boat up between
                // network updates. If the body is kinematic the game drives it and this is skipped.
                if (!HasSync || Ship.rb.isKinematic)
                    return;
                want = SyncedDepth;
            }
            HoldDepth(want);
        }

        private void HoldDepth(float want)
        {
            Rigidbody rb = Ship.rb;
            float dt = Time.fixedDeltaTime;
            float depth = Depth;

            if (want < 1f && depth < 3f)
            {
                // Surfaced: let the stock buoyancy float the hull.
                _haveLast = false;
                _disturbance = 0f;
                return;
            }

            float vmax = SubConfig.VerticalSpeed.Value;
            // Positive vy = up. Too deep -> climb.
            float cmdVy = Mathf.Clamp((depth - want) * 0.12f, -vmax, vmax);
            Vector3 v = rb.velocity;

            if (_haveLast)
            {
                // Whatever moved us away from the last commanded vertical speed is disturbance
                // (buoyancy of the drowned hull, waves). Integrate it so we can cancel it.
                float residual = (v.y - _lastCmdVy) / dt;
                _disturbance = Mathf.Clamp(_disturbance + residual * 0.2f, -400f, 400f);
            }

            v.y = cmdVy;
            rb.velocity = v;
            rb.AddForce(Vector3.up * -_disturbance, ForceMode.Acceleration);
            _lastCmdVy = cmdVy;
            _haveLast = true;

            // Keep the boat level: drowned superstructure would otherwise roll it.
            Vector3 up = Ship.transform.up;
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            Vector3 w = rb.angularVelocity;
            Vector3 wTilt = w - Vector3.Project(w, Vector3.up);
            rb.AddTorque(axis * 4f - wTilt * 2.5f, ForceMode.Acceleration);
        }
    }
}
