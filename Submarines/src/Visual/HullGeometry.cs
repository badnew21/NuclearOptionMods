using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Visual
{
    /// <summary>
    /// Raw triangle soup for the submarine model, split into two materials (hull, detail).
    /// Pure math on purpose: no Unity objects, so every peer builds the identical model from code
    /// and it can be unit-tested / exported outside the game.
    /// </summary>
    internal sealed class MeshData
    {
        internal const int Hull = 0;
        internal const int Detail = 1;

        internal readonly List<Vector3> Verts = new List<Vector3>();
        internal readonly List<int>[] Tris = { new List<int>(), new List<int>() };

        private int _partSub;
        private int _partStart;

        internal int Add(Vector3 v)
        {
            Verts.Add(v);
            return Verts.Count - 1;
        }

        internal void Tri(int sub, int a, int b, int c)
        {
            List<int> t = Tris[sub];
            t.Add(a);
            t.Add(b);
            t.Add(c);
        }

        internal void BeginPart(int sub)
        {
            _partSub = sub;
            _partStart = Tris[sub].Count;
        }

        /// <summary>
        /// Every part is a closed solid. If its signed volume came out negative the winding is inside
        /// out, so flip it. Unity treats Cross(b-a, c-a) as the visible side, which for a closed solid
        /// means positive signed volume.
        /// </summary>
        internal void EndPart()
        {
            List<int> t = Tris[_partSub];
            double vol = 0;
            for (int i = _partStart; i + 2 < t.Count; i += 3)
            {
                Vector3 a = Verts[t[i]], b = Verts[t[i + 1]], c = Verts[t[i + 2]];
                vol += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            if (vol >= 0)
                return;
            for (int i = _partStart; i + 2 < t.Count; i += 3)
            {
                int tmp = t[i + 1];
                t[i + 1] = t[i + 2];
                t[i + 2] = tmp;
            }
        }

        /// <summary>Connect consecutive closed rings of equal size.</summary>
        internal void Strip(int sub, List<int[]> rings)
        {
            for (int r = 0; r + 1 < rings.Count; r++)
            {
                int[] a = rings[r], b = rings[r + 1];
                int n = a.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Tri(sub, a[i], b[i], b[j]);
                    Tri(sub, a[i], b[j], a[j]);
                }
            }
        }

        internal void Fan(int sub, int center, int[] ring)
        {
            int n = ring.Length;
            for (int i = 0; i < n; i++)
                Tri(sub, center, ring[i], ring[(i + 1) % n]);
        }

        internal int VertexCount { get { return Verts.Count; } }
    }

    /// <summary>Dimensions and layout of one submarine class.</summary>
    internal sealed class HullPlan
    {
        internal float Length;
        internal float Radius;
        internal float CenterY;          // hull axis relative to the donor waterline (unit origin)
        internal float SailU;            // 0 = stern, 1 = bow
        internal float SailChord;
        internal float SailHeight;       // above the top of the pressure hull
        internal float SailThickness;    // thickness / chord
        internal float SternFinU;
        internal float SternFinChord;
        internal float SternFinSpan;
        internal bool PumpJet;
        internal int PropBlades;
        internal float HumpHeight;       // SSBN missile deck, as a fraction of Radius
        internal float HumpStartU;
        internal float HumpEndU;
        internal int HatchesPerSide;

        internal static readonly HullPlan Attack = new HullPlan
        {
            Length = 110f,
            Radius = 5.2f,
            CenterY = -3.9f,
            SailU = 0.70f,
            SailChord = 13f,
            SailHeight = 8.5f,
            SailThickness = 0.22f,
            SternFinU = 0.16f,
            SternFinChord = 7f,
            SternFinSpan = 4.6f,
            PumpJet = true,
            PropBlades = 0,
            HumpHeight = 0f,
        };

        internal static readonly HullPlan Ballistic = new HullPlan
        {
            Length = 170f,
            Radius = 6.5f,
            CenterY = -4.9f,
            SailU = 0.76f,
            SailChord = 15f,
            SailHeight = 9f,
            SailThickness = 0.2f,
            SternFinU = 0.15f,
            SternFinChord = 8.5f,
            SternFinSpan = 5.4f,
            PumpJet = false,
            PropBlades = 7,
            HumpHeight = 0.32f,
            HumpStartU = 0.30f,
            HumpEndU = 0.64f,
            HatchesPerSide = 6,
        };

        internal float Beam { get { return Radius * 2f; } }

        /// <summary>How far the bottom of the hull sits below the unit's origin (its waterline).</summary>
        internal float KeelBelowOrigin { get { return Radius - CenterY; } }
        internal float Height { get { return Radius * 2f + SailHeight; } }
    }

    internal static class HullGeometry
    {
        private const int Around = 28;

        internal static MeshData Build(HullPlan p)
        {
            MeshData m = new MeshData();
            BuildPressureHull(m, p);
            BuildSail(m, p);
            BuildSternFins(m, p);
            if (p.PumpJet)
                BuildPumpJet(m, p);
            else
                BuildPropeller(m, p);
            if (p.HatchesPerSide > 0)
                BuildHatches(m, p);
            return m;
        }

        // ------------------------------------------------------------ hull

        /// <summary>Base hull radius: elliptical bow, parallel midbody, long tapered stern.</summary>
        internal static float BaseRadius(HullPlan p, float u)
        {
            if (u >= 0.86f)
            {
                float s = (u - 0.86f) / 0.14f;
                return p.Radius * Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            }
            if (u <= 0.36f)
            {
                float s = u / 0.36f;
                float k = 1f - (1f - s) * (1f - s);
                return p.Radius * (0.12f + 0.88f * k);
            }
            return p.Radius;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Radius at stern-to-bow position u and roll angle theta (0 = top).</summary>
        internal static float Radius(HullPlan p, float u, float theta)
        {
            float r = BaseRadius(p, u);
            if (p.HumpHeight > 0f)
            {
                float w = Smooth(p.HumpStartU - 0.05f, p.HumpStartU, u) * (1f - Smooth(p.HumpEndU, p.HumpEndU + 0.06f, u));
                float c = Mathf.Max(0f, Mathf.Cos(theta));
                r += p.HumpHeight * p.Radius * w * c * c * c;
            }
            return r;
        }

        internal static float Z(HullPlan p, float u) { return (u - 0.5f) * p.Length; }

        internal static Vector3 HullPoint(HullPlan p, float u, float theta)
        {
            float r = Radius(p, u, theta);
            return new Vector3(r * Mathf.Sin(theta), p.CenterY + r * Mathf.Cos(theta), Z(p, u));
        }

        private static void BuildPressureHull(MeshData m, HullPlan p)
        {
            m.BeginPart(MeshData.Hull);
            List<int[]> rings = new List<int[]>();
            const int steps = 72;
            for (int s = 0; s < steps; s++)
            {
                // Denser sampling at both ends where the curvature is.
                float t = s / (float)steps;
                float u = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                int[] ring = new int[Around];
                for (int i = 0; i < Around; i++)
                {
                    float th = i * 2f * Mathf.PI / Around;
                    ring[i] = m.Add(HullPoint(p, u, th));
                }
                rings.Add(ring);
            }
            m.Strip(MeshData.Hull, rings);
            int nose = m.Add(new Vector3(0f, p.CenterY, Z(p, 1f)));
            int[] last = rings[rings.Count - 1];
            int[] lastRev = Reverse(last);
            m.Fan(MeshData.Hull, nose, lastRev);
            int tail = m.Add(new Vector3(0f, p.CenterY, Z(p, 0f) - 0.6f));
            m.Fan(MeshData.Hull, tail, rings[0]);
            m.EndPart();
        }

        // ------------------------------------------------------------ appendages

        /// <summary>Half-thickness of a symmetric NACA 4-digit section at chord fraction x.</summary>
        private static float Naca(float x, float t)
        {
            return 5f * t * (0.2969f * Mathf.Sqrt(x) - 0.1260f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1036f * x * x * x * x);
        }

        private static Vector3[] Section(Vector3 center, Vector3 chordDir, Vector3 thickDir, float chord, float t)
        {
            const int half = 12;
            Vector3[] pts = new Vector3[half * 2];
            for (int i = 0; i < half; i++)
            {
                float x = 0.5f - 0.5f * Mathf.Cos(i * Mathf.PI / (half - 1));
                float y = Mathf.Max(Naca(x, t), 0.004f);
                Vector3 along = chordDir * (chord * (0.5f - x));
                pts[i] = center + along + thickDir * (y * chord);
                pts[half * 2 - 1 - i] = center + along - thickDir * (y * chord);
            }
            return pts;
        }

        /// <summary>
        /// A tapered, swept foil: sail, planes, rudders, propeller blades. chordDir points at the
        /// leading edge.
        /// </summary>
        internal static void Fin(MeshData m, int sub, Vector3 root, Vector3 chordDir, Vector3 spanDir, float chord,
                                 float span, float t, float taper, float sweep)
        {
            chordDir = chordDir.normalized;
            spanDir = spanDir.normalized;
            Vector3 thick = Vector3.Cross(spanDir, chordDir).normalized;
            m.BeginPart(sub);
            List<int[]> rings = new List<int[]>();
            const int sections = 4;
            for (int s = 0; s <= sections; s++)
            {
                float f = s / (float)sections;
                float c = chord * Mathf.Lerp(1f, taper, f);
                Vector3 center = root + spanDir * (span * f) - chordDir * (sweep * span * f);
                Vector3[] pts = Section(center, chordDir, thick, c, t);
                int[] ring = new int[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                    ring[i] = m.Add(pts[i]);
                rings.Add(ring);
            }
            m.Strip(sub, rings);
            int[] first = rings[0], tip = rings[rings.Count - 1];
            m.Fan(sub, m.Add(Centroid(m, first)), first);
            m.Fan(sub, m.Add(Centroid(m, tip)), Reverse(tip));
            m.EndPart();
        }

        private static void BuildSail(MeshData m, HullPlan p)
        {
            float zc = Z(p, p.SailU);
            float topOfHull = p.CenterY + Radius(p, p.SailU, 0f);
            Vector3 root = new Vector3(0f, topOfHull - 1.2f, zc);
            float span = p.SailHeight + 1.2f;
            Fin(m, MeshData.Hull, root, Vector3.forward, Vector3.up, p.SailChord, span, p.SailThickness, 0.88f, 0.12f);

            // Fairwater planes on the sail.
            float planeY = topOfHull + p.SailHeight * 0.62f;
            float planeZ = zc + p.SailChord * 0.12f;
            float halfThick = Naca(0.3f, p.SailThickness) * p.SailChord;
            float pChord = p.SailChord * 0.32f;
            float pSpan = p.Radius * 0.85f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 r = new Vector3(side * halfThick * 0.6f, planeY, planeZ);
                Fin(m, MeshData.Hull, r, Vector3.forward, new Vector3(side, 0f, 0f), pChord, pSpan, 0.12f, 0.7f, 0.15f);
            }

            // Periscope and mast stubs.
            for (int i = 0; i < 3; i++)
            {
                float z = zc + p.SailChord * (0.15f - i * 0.12f);
                Cylinder(m, MeshData.Detail, new Vector3(0f, topOfHull + p.SailHeight - 0.4f, z), Vector3.up,
                         0.22f + 0.06f * i, 1.4f + 0.7f * (2 - i), 10);
            }
        }

        private static void BuildSternFins(MeshData m, HullPlan p)
        {
            float u = p.SternFinU;
            float z = Z(p, u);
            float[] angles = { 0f, 90f, 180f, 270f };
            for (int i = 0; i < angles.Length; i++)
            {
                float th = angles[i] * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
                float r = BaseRadius(p, u) - 0.4f;
                Vector3 root = new Vector3(0f, p.CenterY, z) + dir * r;
                float span = p.SternFinSpan + 0.4f;
                // The lower rudder is shorter so the boat can sit on a pier.
                if (angles[i] == 180f)
                    span *= 0.8f;
                Fin(m, MeshData.Hull, root, Vector3.forward, dir, p.SternFinChord, span, 0.1f, 0.55f, 0.45f);
            }
        }

        private static void BuildPumpJet(MeshData m, HullPlan p)
        {
            float z0 = Z(p, 0f) - 1.2f;
            float z1 = Z(p, 0.055f);
            float rOut = p.Radius * 0.56f;
            float rIn = p.Radius * 0.47f;
            // Duct: closed tube, slightly converging toward the exit.
            m.BeginPart(MeshData.Hull);
            List<int[]> rings = new List<int[]>();
            float[] zs = { z1, z1 - 0.3f, z0 + 0.3f, z0, z0, z1 };
            float[] rs = { (rOut + rIn) * 0.5f, rOut, rOut * 0.93f, (rOut + rIn) * 0.47f, rIn * 0.9f, rIn };
            for (int k = 0; k < zs.Length; k++)
            {
                int[] ring = new int[Around];
                for (int i = 0; i < Around; i++)
                {
                    float th = i * 2f * Mathf.PI / Around;
                    ring[i] = m.Add(new Vector3(rs[k] * Mathf.Sin(th), p.CenterY + rs[k] * Mathf.Cos(th), zs[k]));
                }
                rings.Add(ring);
            }
            rings.Add(rings[0]);
            m.Strip(MeshData.Hull, rings);
            m.EndPart();

            // Stator vanes tying the duct to the hull.
            for (int i = 0; i < 6; i++)
            {
                float th = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
                float zc = (z0 + z1) * 0.5f + 0.8f;
                float rh = BaseRadius(p, 0.02f) * 0.8f;
                Fin(m, MeshData.Detail, new Vector3(0f, p.CenterY, zc) + dir * rh, Vector3.forward, dir,
                    (z1 - z0) * 0.6f, rIn - rh + 0.1f, 0.08f, 1f, 0f);
            }
        }

        private static void BuildPropeller(MeshData m, HullPlan p)
        {
            float zTail = Z(p, 0f);
            float zHub = zTail - 1.6f;
            float hubR = p.Radius * 0.15f;
            Cylinder(m, MeshData.Detail, new Vector3(0f, p.CenterY, zHub - 0.9f), Vector3.forward, hubR, 2.4f, 14);
            float bladeSpan = p.Radius * 0.62f;
            for (int i = 0; i < p.PropBlades; i++)
            {
                float th = i * 2f * Mathf.PI / p.PropBlades;
                Vector3 radial = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
                Vector3 tangent = new Vector3(Mathf.Cos(th), -Mathf.Sin(th), 0f);
                // 32 degrees of pitch: the chord leans between tangential and axial.
                Vector3 chord = (tangent * Mathf.Cos(32f * Mathf.Deg2Rad) + Vector3.forward * Mathf.Sin(32f * Mathf.Deg2Rad)).normalized;
                Vector3 root = new Vector3(0f, p.CenterY, zHub) + radial * (hubR * 0.8f);
                Fin(m, MeshData.Detail, root, chord, radial, 1.9f, bladeSpan, 0.09f, 0.55f, 0.35f);
            }
        }

        private static void BuildHatches(MeshData m, HullPlan p)
        {
            float span = p.HumpEndU - p.HumpStartU;
            for (int i = 0; i < p.HatchesPerSide; i++)
            {
                float u = p.HumpStartU + span * (0.12f + 0.76f * i / Mathf.Max(1, p.HatchesPerSide - 1));
                for (int side = -1; side <= 1; side += 2)
                {
                    float th = side * 0.27f;
                    Vector3 pt = HullPoint(p, u, th);
                    Vector3 n = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
                    Cylinder(m, MeshData.Detail, pt - n * 0.08f, n, 1.15f, 0.2f, 18);
                }
            }
        }

        // ------------------------------------------------------------ torpedo

        /// <summary>533 mm heavyweight torpedo, 6.4 m, nose at +Z, centred on the origin.</summary>
        internal static MeshData BuildTorpedo()
        {
            const float len = 6.4f, r0 = 0.267f;
            MeshData m = new MeshData();
            m.BeginPart(MeshData.Hull);
            List<int[]> rings = new List<int[]>();
            const int steps = 24, around = 14;
            for (int s = 0; s < steps; s++)
            {
                float u = s / (float)(steps - 1) * 0.995f;
                float r;
                if (u > 0.9f) { float k = (u - 0.9f) / 0.1f; r = r0 * Mathf.Sqrt(Mathf.Max(0.02f, 1f - k * k)); }
                else if (u < 0.22f) { float k = u / 0.22f; r = r0 * (0.35f + 0.65f * (1f - (1f - k) * (1f - k))); }
                else r = r0;
                int[] ring = new int[around];
                for (int i = 0; i < around; i++)
                {
                    float th = i * 2f * Mathf.PI / around;
                    ring[i] = m.Add(new Vector3(r * Mathf.Sin(th), r * Mathf.Cos(th), (u - 0.5f) * len));
                }
                rings.Add(ring);
            }
            m.Strip(MeshData.Hull, rings);
            m.Fan(MeshData.Hull, m.Add(new Vector3(0f, 0f, len * 0.5f)), Reverse(rings[rings.Count - 1]));
            m.Fan(MeshData.Hull, m.Add(new Vector3(0f, 0f, -len * 0.5f - 0.05f)), rings[0]);
            m.EndPart();
            for (int i = 0; i < 4; i++)
            {
                float th = (45f + 90f * i) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
                Fin(m, MeshData.Detail, new Vector3(0f, 0f, -len * 0.42f) + dir * 0.12f, Vector3.forward, dir, 0.45f, 0.22f, 0.1f, 0.7f, 0.3f);
            }
            Cylinder(m, MeshData.Detail, new Vector3(0f, 0f, -len * 0.5f - 0.3f), Vector3.forward, 0.1f, 0.3f, 10);
            return m;
        }

        // ------------------------------------------------------------ primitives

        /// <summary>Closed cylinder from base along axis.</summary>
        internal static void Cylinder(MeshData m, int sub, Vector3 basePt, Vector3 axis, float radius, float length, int n)
        {
            axis = axis.normalized;
            Vector3 a = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 e1 = Vector3.Cross(axis, a).normalized;
            Vector3 e2 = Vector3.Cross(axis, e1).normalized;
            m.BeginPart(sub);
            int[] lo = new int[n], hi = new int[n];
            for (int i = 0; i < n; i++)
            {
                float th = i * 2f * Mathf.PI / n;
                Vector3 off = (e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th)) * radius;
                lo[i] = m.Add(basePt + off);
                hi[i] = m.Add(basePt + axis * length + off);
            }
            List<int[]> rings = new List<int[]> { lo, hi };
            m.Strip(sub, rings);
            m.Fan(sub, m.Add(basePt), lo);
            m.Fan(sub, m.Add(basePt + axis * length), Reverse(hi));
            m.EndPart();
        }

        private static int[] Reverse(int[] r)
        {
            int[] o = new int[r.Length];
            for (int i = 0; i < r.Length; i++)
                o[i] = r[r.Length - 1 - i];
            return o;
        }

        private static Vector3 Centroid(MeshData m, int[] ring)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < ring.Length; i++)
                c += m.Verts[ring[i]];
            return c / ring.Length;
        }
    }
}
