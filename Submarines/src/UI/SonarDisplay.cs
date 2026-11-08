using System.Collections.Generic;
using NuclearOption.Networking;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.UI
{
    /// <summary>
    /// IMGUI sonar plot for the local player's submarines (F8). North-up, centred on the selected
    /// boat. Shows what the boat's sonar believes, so enemy decoys and phantoms show up here as
    /// ordinary contacts. Orders work on the host / in single player.
    /// </summary>
    internal static class SonarDisplay
    {
        private static bool _visible;
        private static int _index;
        private static int _rangeStep = 1;
        private static readonly float[] Ranges = { 10000f, 30000f, 60000f };
        private static string _msg = string.Empty;
        private static float _msgUntil;
        private static Texture2D _px;
        private static readonly List<Submarine> Mine = new List<Submarine>();

        private const float Radius = 150f;

        internal static void TickInput()
        {
            if (Input.GetKeyDown(SubConfig.DisplayKey.Value))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                    _rangeStep = (_rangeStep + 1) % Ranges.Length;
                else
                    _visible = !_visible;
            }
            if (!_visible)
                return;

            CollectMine();
            if (Mine.Count == 0)
                return;
            if (Input.GetKeyDown(SubConfig.NextSubKey.Value))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (Net.IsServer)
                    {
                        Selected().ClearManual();
                        Say("Released to AI control.");
                    }
                }
                else
                {
                    _index = (_index + 1) % Mine.Count;
                }
            }

            Submarine s = Selected();
            if (s == null)
                return;

            bool any = Input.GetKeyDown(SubConfig.DeeperKey.Value) || Input.GetKeyDown(SubConfig.ShallowerKey.Value) ||
                       Input.GetKeyDown(SubConfig.TorpedoKey.Value) || Input.GetKeyDown(SubConfig.DecoyKey.Value) ||
                       Input.GetKeyDown(SubConfig.MimicKey.Value) || Input.GetKeyDown(SubConfig.LaunchKey.Value) ||
                       Input.GetKeyDown(SubConfig.PingKey.Value);
            if (!any)
                return;
            if (!Net.IsServer)
            {
                Say("Orders are host-only in multiplayer.");
                return;
            }

            if (Input.GetKeyDown(SubConfig.DeeperKey.Value))
            {
                s.OrderDepth(Mathf.Max(s.OrderedDepth, 0f) + 20f);
                Say("Make depth " + s.OrderedDepth.ToString("F0") + " m.");
            }
            if (Input.GetKeyDown(SubConfig.ShallowerKey.Value))
            {
                float d = s.OrderedDepth - 20f;
                if (d < SubConfig.PeriscopeDepth.Value)
                    d = d < SubConfig.PeriscopeDepth.Value - 10f ? 0f : SubConfig.PeriscopeDepth.Value;
                s.OrderDepth(d);
                Say(d <= 0f ? "Surface, surface." : "Make depth " + d.ToString("F0") + " m.");
            }
            if (Input.GetKeyDown(SubConfig.TorpedoKey.Value))
            {
                SonarContact c = s.Sonar != null ? s.Sonar.BestTarget(SubConfig.TorpedoRange.Value, 0.15f, true, true) : null;
                if (c == null)
                    Say("No firing solution.");
                else
                    Say(s.FireTorpedo(c) ? "Torpedo away on " + Acoustics.Label(c.Class) + " contact." : "Tube not ready.");
            }
            if (Input.GetKeyDown(SubConfig.DecoyKey.Value))
                Say(s.LaunchDecoy() ? "Countermeasure launched." : "No decoy ready.");
            if (Input.GetKeyDown(SubConfig.MimicKey.Value))
                Say(s.EngageMimic() ? "Acoustic mimicry engaged." : "Mimicry recharging.");
            if (Input.GetKeyDown(SubConfig.LaunchKey.Value))
            {
                if (s.Spec.Role != SubRole.Ballistic)
                    Say("No missile tubes on this boat.");
                else if (s.Missiles <= 0)
                    Say("Tubes empty.");
                else if (s.FindStrategicTarget() == null)
                    Say("No known target in range.");
                else
                {
                    s.PendingLaunch = true;
                    Say("Coming to launch depth.");
                }
            }
            if (Input.GetKeyDown(SubConfig.PingKey.Value) && s.Sonar != null)
            {
                s.Sonar.RequestPing();
                Say("Ping! (we are counter-detectable now)");
            }
        }

        private static void Say(string m)
        {
            _msg = m;
            _msgUntil = Time.unscaledTime + 4f;
        }

        private static Submarine Selected()
        {
            if (Mine.Count == 0)
                return null;
            if (_index >= Mine.Count)
                _index = 0;
            return Mine[_index];
        }

        private static void CollectMine()
        {
            Mine.Clear();
            FactionHQ local = null;
            Player p;
            if (GameManager.GetLocalPlayer(out p) && p != null)
                local = p.HQ;
            List<Submarine> subs = UnderwaterWorld.Subs;
            for (int i = 0; i < subs.Count; i++)
            {
                Submarine s = subs[i];
                if (s == null || s.Ship == null || s.Ship.disabled)
                    continue;
                if (local == null || s.Ship.NetworkHQ == local)
                    Mine.Add(s);
            }
        }

        internal static void Draw()
        {
            if (!_visible)
                return;
            if (_px == null)
            {
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }

            CollectMine();
            Rect box = new Rect(12f, Screen.height - 470f, 340f, 458f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.03f, 0.85f);
            GUI.DrawTexture(box, _px);
            GUI.color = new Color(0.4f, 1f, 0.6f, 1f);

            Submarine s = Selected();
            if (s == null)
            {
                GUI.Label(new Rect(box.x + 10f, box.y + 10f, 320f, 40f), "SONAR\nNo friendly submarines.");
                GUI.color = oldColor;
                return;
            }

            float range = Ranges[_rangeStep];
            Vector2 c = new Vector2(box.x + box.width * 0.5f, box.y + 30f + Radius);
            Ring(c, Radius, new Color(0.2f, 0.6f, 0.3f, 0.8f));
            Ring(c, Radius * 0.5f, new Color(0.2f, 0.6f, 0.3f, 0.4f));

            Vector3 me = Geo.GlobalOf(s.Ship);
            Vector3 fwd = s.Ship.transform.forward;
            Dot(c + new Vector2(fwd.x, -fwd.z) * 18f, 3f, new Color(0.4f, 1f, 0.6f));
            Dot(c, 5f, new Color(0.4f, 1f, 0.6f));

            if (s.Sonar != null)
            {
                foreach (SonarContact k in s.Sonar.Contacts)
                {
                    Vector3 d = k.EstimatedPos - me;
                    Vector2 pt = new Vector2(d.x, -d.z) / range * Radius;
                    if (pt.magnitude > Radius)
                        pt = pt.normalized * Radius;
                    Color col = ClassColor(k.Class);
                    if (Time.time - k.LastSeen > 4f)
                        col.a = 0.45f;
                    Dot(c + pt, 4f, col);
                    string label = Acoustics.Label(k.Class) + " " + (k.Quality * 100f).ToString("F0") + "%";
                    if (SubConfig.ShowTruth.Value && k.IsFalse && k.Class != ContactClass.Torpedo)
                        label += " [FALSE]";
                    GUI.color = col;
                    GUI.Label(new Rect(c.x + pt.x + 5f, c.y + pt.y - 9f, 120f, 20f), label);
                }
            }

            // Our own countermeasures and fish (server knows them).
            List<Decoy> decoys = UnderwaterWorld.Decoys;
            for (int i = 0; i < decoys.Count; i++)
            {
                Decoy dc = decoys[i];
                if (!dc.Alive || dc.Faction != s.Ship.NetworkHQ)
                    continue;
                Vector3 d = dc.NoisePos - me;
                Dot(c + Clamp(new Vector2(d.x, -d.z) / range * Radius), 3f, new Color(0.3f, 0.9f, 1f));
            }
            List<Torpedo> torps = UnderwaterWorld.Torpedoes;
            for (int i = 0; i < torps.Count; i++)
            {
                Torpedo t = torps[i];
                if (t.Dead || t.Faction != s.Ship.NetworkHQ)
                    continue;
                Vector3 d = t.Pos - me;
                Dot(c + Clamp(new Vector2(d.x, -d.z) / range * Radius), 3f, new Color(0.6f, 1f, 0.2f));
            }

            GUI.color = new Color(0.4f, 1f, 0.6f, 1f);
            string stealth = s.Submerged
                ? (s.MastsExposed ? "PERISCOPE DEPTH - masts exposed" : "SUBMERGED - radar invisible")
                : "SURFACED - visible";
            string mimic = s.MimicActive ? "ACTIVE" : (s.MimicCooldownLeft > 0f ? s.MimicCooldownLeft.ToString("F0") + "s" : "ready");
            string text =
                s.Spec.UnitName + "  [" + (_index + 1) + "/" + Mine.Count + "]  " + (range / 1000f).ToString("F0") + " km\n" +
                "Depth " + s.Depth.ToString("F0") + " m  (ordered " + s.OrderedDepth.ToString("F0") + ")  " + s.Mode + "\n" +
                stealth + "\n" +
                "Torps " + s.Torpedoes + "  SLBM " + s.Missiles + "  Decoys " + s.DecoysLeft + "  Mimic " + mimic;
            GUI.Label(new Rect(box.x + 10f, c.y + Radius + 6f, 320f, 70f), text);
            GUI.Label(new Rect(box.x + 10f, box.y + 4f, 320f, 22f),
                SubConfig.NextSubKey.Value + " next  " + SubConfig.DeeperKey.Value + "/" + SubConfig.ShallowerKey.Value + " depth  " +
                SubConfig.TorpedoKey.Value + " torp  " + SubConfig.DecoyKey.Value + " decoy");
            if (Time.unscaledTime < _msgUntil)
                GUI.Label(new Rect(box.x + 10f, box.y + box.height - 22f, 320f, 22f), _msg);
            GUI.color = oldColor;
        }

        private static Vector2 Clamp(Vector2 p)
        {
            return p.magnitude > Radius ? p.normalized * Radius : p;
        }

        private static Color ClassColor(ContactClass k)
        {
            switch (k)
            {
                case ContactClass.Submarine: return new Color(1f, 0.25f, 0.2f);
                case ContactClass.Surface: return new Color(1f, 0.65f, 0.2f);
                case ContactClass.Torpedo: return Time.unscaledTime % 0.6f < 0.3f ? Color.magenta : Color.white;
                case ContactClass.Merchant: return new Color(0.7f, 0.7f, 0.7f);
                case ContactClass.Decoy: return new Color(0.5f, 0.5f, 0.55f);
                default: return new Color(0.9f, 0.9f, 0.5f);
            }
        }

        private static void Dot(Vector2 p, float size, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), _px);
        }

        private static void Ring(Vector2 c, float r, Color col)
        {
            for (int i = 0; i < 72; i++)
            {
                float a = i * 5f * Mathf.Deg2Rad;
                Dot(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r, 2f, col);
            }
        }
    }
}
