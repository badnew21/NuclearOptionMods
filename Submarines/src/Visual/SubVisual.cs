using System.Collections.Generic;
using UnityEngine;

namespace Submarines.Visual
{
    /// <summary>
    /// Puts the procedural submarine model on a (cloned) ship prefab and hides the donor hull.
    ///
    /// The model is generated from code (HullGeometry) on every machine, so anyone in the lobby
    /// with the mod installed sees the same boat. Nothing needs to be downloaded or synced.
    /// </summary>
    internal static class SubVisual
    {
        internal const string ModelName = "SubmarineModel";

        private static readonly Dictionary<HullPlan, Mesh> Meshes = new Dictionary<HullPlan, Mesh>();
        private static Mesh _torpedoMesh;
        private static Material _hullMat;
        private static Material _detailMat;

        internal static HullPlan PlanFor(SubSpec spec)
        {
            return spec.Role == SubRole.Ballistic ? HullPlan.Ballistic : HullPlan.Attack;
        }

        internal static void Apply(GameObject root, SubSpec spec)
        {
            if (root == null || spec == null)
                return;
            HullPlan plan = PlanFor(spec);
            HideDonor(root);

            Transform existing = root.transform.Find(ModelName);
            if (existing != null)
                return;

            GameObject model = new GameObject(ModelName);
            model.layer = root.layer;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, SubConfig.ModelVerticalOffset.Value, 0f);
            model.transform.localRotation = Quaternion.identity;

            MeshFilter mf = model.AddComponent<MeshFilter>();
            mf.sharedMesh = MeshFor(plan);
            MeshRenderer mr = model.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials(root);

            if (root.GetComponent<DonorHider>() == null)
                root.AddComponent<DonorHider>();
        }

        /// <summary>Turn off everything the donor ship draws or emits. Colliders and logic stay.</summary>
        internal static void HideDonor(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && !IsOurs(r.transform, root.transform))
                    r.enabled = false;
            }
            foreach (LODGroup lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                if (lod != null)
                    lod.enabled = false;
            }
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null)
                    continue;
                ParticleSystem.EmissionModule em = ps.emission;
                em.enabled = false;
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l != null)
                    l.enabled = false;
            }
        }

        internal static bool IsOurs(Transform t, Transform root)
        {
            while (t != null && t != root)
            {
                if (t.name == ModelName || t.name == TorpedoVisuals.Name)
                    return true;
                t = t.parent;
            }
            return false;
        }

        internal static Mesh MeshFor(HullPlan plan)
        {
            Mesh mesh;
            if (Meshes.TryGetValue(plan, out mesh) && mesh != null)
                return mesh;
            mesh = ToMesh(HullGeometry.Build(plan), plan == HullPlan.Ballistic ? "SSBN_Leviathan" : "SSN_Kestrel");
            Meshes[plan] = mesh;
            return mesh;
        }

        internal static Mesh TorpedoMesh()
        {
            if (_torpedoMesh == null)
                _torpedoMesh = ToMesh(HullGeometry.BuildTorpedo(), "Torpedo_533");
            return _torpedoMesh;
        }

        private static Mesh ToMesh(MeshData d, string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(d.Verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(d.Tris[MeshData.Hull], MeshData.Hull);
            mesh.SetTriangles(d.Tris[MeshData.Detail], MeshData.Detail);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Object.DontDestroyOnLoad(mesh);
            return mesh;
        }

        // ------------------------------------------------------------ materials

        internal static Material[] Materials(GameObject sample)
        {
            if (_hullMat == null || _detailMat == null)
            {
                Material template = FindTemplate(sample);
                // Anechoic tiles: almost black, a little satin.
                _hullMat = Make(template, "Sub_Hull", new Color(0.055f, 0.06f, 0.066f), 0.32f);
                _detailMat = Make(template, "Sub_Detail", new Color(0.16f, 0.165f, 0.17f), 0.45f);
            }
            return new[] { _hullMat, _detailMat };
        }

        /// <summary>
        /// The game renders with URP, so the built-in Standard shader draws nothing. Copy a lit
        /// material the donor already uses, then strip its textures.
        /// </summary>
        private static Material FindTemplate(GameObject sample)
        {
            if (sample != null)
            {
                foreach (MeshRenderer r in sample.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material m = r != null ? r.sharedMaterial : null;
                    if (m != null && m.shader != null && IsLit(m.shader.name))
                        return m;
                }
            }
            return null;
        }

        private static bool IsLit(string shader)
        {
            if (string.IsNullOrEmpty(shader))
                return false;
            string s = shader.ToLowerInvariant();
            return s.Contains("lit") && !s.Contains("unlit") && !s.Contains("particle") && !s.Contains("water");
        }

        private static Material Make(Material template, string name, Color color, float smoothness)
        {
            Material mat;
            if (template != null)
            {
                mat = new Material(template);
            }
            else
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null)
                    sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh == null)
                    sh = Shader.Find("Standard");
                mat = new Material(sh);
            }
            mat.name = name;
            string[] maps = { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_SpecGlossMap", "_ParallaxMap" };
            for (int i = 0; i < maps.Length; i++)
            {
                if (mat.HasProperty(maps[i]))
                    mat.SetTexture(maps[i], null);
            }
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.15f);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.DisableKeyword("_NORMALMAP");
            Object.DontDestroyOnLoad(mat);
            return mat;
        }
    }

    /// <summary>
    /// Some stock code (damage states, LOD) re-enables renderers at runtime. Keep the donor hull
    /// hidden for the life of each spawned submarine.
    /// </summary>
    internal sealed class DonorHider : MonoBehaviour
    {
        private Renderer[] _donor;
        private float _next;

        private void Start()
        {
            List<Renderer> list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && !SubVisual.IsOurs(r.transform, transform))
                    list.Add(r);
            }
            _donor = list.ToArray();
            SubVisual.HideDonor(gameObject);
        }

        private void LateUpdate()
        {
            if (_donor == null || Time.time < _next)
                return;
            _next = Time.time + 0.5f;
            for (int i = 0; i < _donor.Length; i++)
            {
                if (_donor[i] != null && _donor[i].enabled)
                    _donor[i].enabled = false;
            }
        }
    }
}
