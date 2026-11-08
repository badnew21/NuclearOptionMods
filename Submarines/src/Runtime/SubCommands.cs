using UnityEngine;

namespace Submarines.Runtime
{
    internal enum SubOrder : byte
    {
        Deeper,
        Shallower,
        Release,
        Torpedo,
        Decoy,
        Mimic,
        Launch,
        Ping,
    }

    internal enum SubNotice : byte
    {
        None,
        MakeDepth,
        Surface,
        Released,
        TorpedoAway,
        NoSolution,
        TubeNotReady,
        DecoyAway,
        NoDecoy,
        MimicOn,
        MimicRecharging,
        NoTubes,
        TubesEmpty,
        NoTarget,
        LaunchDepth,
        Ping,
        NotYourBoat,
        Sent,
    }

    /// <summary>
    /// Player orders. Executed on the server, whether they came from the host's keyboard or from a
    /// client over SubNet, so every peer sees the same result.
    /// </summary>
    internal static class SubCommands
    {
        internal static SubNotice Execute(Submarine s, SubOrder order, out float value)
        {
            value = 0f;
            if (s == null)
                return SubNotice.None;
            switch (order)
            {
                case SubOrder.Deeper:
                    s.OrderDepth(Mathf.Max(s.OrderedDepth, 0f) + 20f);
                    value = s.OrderedDepth;
                    return SubNotice.MakeDepth;

                case SubOrder.Shallower:
                {
                    float d = s.OrderedDepth - 20f;
                    float peri = SubConfig.PeriscopeDepth.Value;
                    if (d < peri)
                        d = d < peri - 10f ? 0f : peri;
                    s.OrderDepth(d);
                    value = d;
                    return d <= 0f ? SubNotice.Surface : SubNotice.MakeDepth;
                }

                case SubOrder.Release:
                    s.ClearManual();
                    return SubNotice.Released;

                case SubOrder.Torpedo:
                {
                    SonarContact c = s.Sonar != null ? s.Sonar.BestTarget(SubConfig.TorpedoRange.Value, 0.15f, true, true) : null;
                    if (c == null)
                        return SubNotice.NoSolution;
                    return s.FireTorpedo(c) ? SubNotice.TorpedoAway : SubNotice.TubeNotReady;
                }

                case SubOrder.Decoy:
                    return s.LaunchDecoy() ? SubNotice.DecoyAway : SubNotice.NoDecoy;

                case SubOrder.Mimic:
                    return s.EngageMimic() ? SubNotice.MimicOn : SubNotice.MimicRecharging;

                case SubOrder.Launch:
                    if (s.Spec.Role != SubRole.Ballistic)
                        return SubNotice.NoTubes;
                    if (s.Missiles <= 0)
                        return SubNotice.TubesEmpty;
                    if (s.FindStrategicTarget() == null)
                        return SubNotice.NoTarget;
                    s.PendingLaunch = true;
                    return SubNotice.LaunchDepth;

                case SubOrder.Ping:
                    if (s.Sonar != null)
                        s.Sonar.RequestPing();
                    return SubNotice.Ping;
            }
            return SubNotice.None;
        }

        internal static string Text(SubNotice n, float value)
        {
            switch (n)
            {
                case SubNotice.MakeDepth: return "Make depth " + value.ToString("F0") + " m.";
                case SubNotice.Surface: return "Surface, surface.";
                case SubNotice.Released: return "Released to AI control.";
                case SubNotice.TorpedoAway: return "Torpedo away.";
                case SubNotice.NoSolution: return "No firing solution.";
                case SubNotice.TubeNotReady: return "Tube not ready.";
                case SubNotice.DecoyAway: return "Countermeasure launched.";
                case SubNotice.NoDecoy: return "No decoy ready.";
                case SubNotice.MimicOn: return "Acoustic mimicry engaged.";
                case SubNotice.MimicRecharging: return "Mimicry recharging.";
                case SubNotice.NoTubes: return "No missile tubes on this boat.";
                case SubNotice.TubesEmpty: return "Tubes empty.";
                case SubNotice.NoTarget: return "No known target in range.";
                case SubNotice.LaunchDepth: return "Coming to launch depth.";
                case SubNotice.Ping: return "Ping! (we are counter-detectable now)";
                case SubNotice.NotYourBoat: return "That boat belongs to another faction.";
                case SubNotice.Sent: return "Order sent to host...";
                default: return string.Empty;
            }
        }

        /// <summary>Run locally on the server, or forward to it from a client.</summary>
        internal static string Issue(Submarine s, SubOrder order)
        {
            if (Net.IsServer)
            {
                float v;
                return Text(Execute(s, order, out v), v);
            }
            Sync.SubNet.SendOrder(s, order);
            return Text(SubNotice.Sent, 0f);
        }
    }
}
