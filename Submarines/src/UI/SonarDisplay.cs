using System.Collections.Generic;
using NuclearOption.Networking;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.UI
{
    /// <summary>
    /// IMGUI sonar plot for the local player's submarines (F8). North-up, centred on the selected
    /// boat. Shows what the boat's sonar believes, so enemy decoys and phantoms show up here as
    /// ordinary contacts. Orders from clients are sent to the host and run there.
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
            CollectMine();
            Submarine s;
            if (_visible)
            {
                s = Selected();
            }
            else
            {
                // Driving a sub yourself (BOTE): its keys work without the sonar screen open.
                s = Piloted();
                if (s == null)
                    return;
                IssueOrders(s);
                return;
            }
            if (Mine.Count == 0 || s == null)
                return;

            if (Input.GetKeyDown(SubConfig.NextSubKey.Value))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    Say(SubCommands.Issue(s, SubOrder.Release));
                else
                    _index = (_index + 1) % Mine.Count;
                return;
            }
            IssueOrders(s);
        }

        private static void IssueOrders(Submarine s)
        {
            if (Input.GetKeyDown(SubConfig.DeeperKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Deeper));
            if (Input.GetKeyDown(SubConfig.ShallowerKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Shallower));
            if (Input.GetKeyDown(SubConfig.TorpedoKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Torpedo));
            if (Input.GetKeyDown(SubConfig.DecoyKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Decoy));
            if (Input.GetKeyDown(SubConfig.MimicKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Mimic));
            if (Input.GetKeyDown(SubConfig.LaunchKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Launch));
            if (Input.GetKeyDown(SubConfig.PingKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Ping));
            if (Input.GetKeyDown(SubConfig.WarheadKey.Value))
                Say(SubCommands.Issue(s, SubOrder.Warhead));
        }

        internal static void Say(string m)
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

        private static Submarine _lastPiloted;

        private static void CollectMine()
        {
            Mine.Clear();
            // The boat you're driving (BOTE) always comes first and is selected when you board it.
            Aircraft mine;
            Submarine piloted = GameManager.GetLocalAircraft(out mine) && mine != null ? mine.GetComponent<Submarine>() : null;
            if (piloted != null && !piloted.Ship.disabled)
                Mine.Add(piloted);
            if (piloted != _lastPiloted)
            {
                _lastPiloted = piloted;
                _index = 0;
            }
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
                if ((local == null || s.Ship.NetworkHQ == local) && s != piloted)
                    Mine.Add(s);
            }
        }

        private static Submarine Piloted()
        {
            return _lastPiloted != null && _lastPiloted.Ship != null && !_lastPiloted.Ship.disabled ? _lastPiloted : null;
        }

        /// <summary>One-line depth readout at the top of the screen while you drive a sub.</summary>
        private static void DrawPilotLine()
        {
            Submarine p = Piloted();
            if (p == null)
                return;
            string state = p.Submerged ? (p.MastsExposed ? "PERISCOPE" : "SUBMERGED") : "SURFACED";
            string line = p.Spec.UnitName + "   " + state + "   depth " + p.Depth.ToString("F0") + " m (ordered " +
                          p.OrderedDepth.ToString("F0") + ")   water " +
                          (p.WaterDepth >= 4999f ? "deep" : p.WaterDepth.ToString("F0") + " m") +
                          "   torps " + p.Torpedoes + (p.Spec.Role == SubRole.Ballistic ? "  SLBM " + p.Missiles : "") +
                          "  decoys " + p.DecoysLeft;
            string hint;
            if (p.DepthLimitedBySeabed)
                hint = p.MaxDepthHere < 1f
                    ? "Too shallow to dive here: head for deeper water."
                    : "Seabed limits depth here to " + p.MaxDepthHere.ToString("F0") + " m.";
            else if (!p.Submerged && p.OrderedDepth < 1f)
                hint = SubConfig.DeeperKey.Value + " dive, " + SubConfig.ShallowerKey.Value + " climb, " +
                       SubConfig.TorpedoKey.Value + " torpedo, " + SubConfig.DecoyKey.Value + " decoy, " +
                       SubConfig.DisplayKey.Value + " sonar";
            else
                hint = string.Empty;
            Rect r = new Rect(Screen.width * 0.5f - 300f, 8f, 600f, 44f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, _px);
            GUI.color = p.DepthLimitedBySeabed ? new Color(1f, 0.75f, 0.3f) : new Color(0.4f, 1f, 0.6f);
            GUI.Label(new Rect(r.x + 8f, r.y + 2f, r.width - 16f, 20f), line);
            GUI.Label(new Rect(r.x + 8f, r.y + 21f, r.width - 16f, 20f), hint.Length > 0 ? hint :
                (Time.unscaledTime < _msgUntil ? _msg : string.Empty));
            GUI.color = old;
        }

        internal static void Draw()
        {
            if (_px == null)
            {
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }
            if (!_visible)
            {
                DrawPilotLine();
                return;
            }

            CollectMine();
            Rect box = new Rect(12f, Screen.height - 492f, 340f, 480f);
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
                "Torps " + s.Torpedoes + "  SLBM " + s.Missiles + "  Decoys " + s.DecoysLeft + "  Mimic " + mimic +
                WarheadLine(s);
            GUI.Label(new Rect(box.x + 10f, c.y + Radius + 6f, 320f, 90f), text);
            GUI.Label(new Rect(box.x + 10f, box.y + 4f, 320f, 22f),
                SubConfig.NextSubKey.Value + " next  " + SubConfig.DeeperKey.Value + "/" + SubConfig.ShallowerKey.Value + " depth  " +
                SubConfig.TorpedoKey.Value + " torp  " + SubConfig.DecoyKey.Value + " decoy");
            if (Time.unscaledTime < _msgUntil)
                GUI.Label(new Rect(box.x + 10f, box.y + box.height - 22f, 320f, 22f), _msg);
            GUI.color = oldColor;
        }

        private static string WarheadLine(Submarine s)
        {
            if (s.Spec.Role != SubRole.Ballistic)
                return string.Empty;
            bool nuclear;
            ReleaseBlock block;
            MissileDefinition next = s.PreviewMissile(out nuclear, out block);
            string state = next == null ? "HOLD (" + Nukes.BlockText(block) + ")"
                : nuclear ? "next: NUCLEAR" : (s.Warhead == WarheadMode.Conventional ? "next: conventional" : "next: conventional (" + Nukes.BlockText(block) + ")");
            return "\nWarhead " + Nukes.Label(s.Warhead) + "  stockpile " + Nukes.Stockpile(s.Ship.NetworkHQ) + "  " + state;
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
