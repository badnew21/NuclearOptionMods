// Runs the mod's real HullGeometry code under mono and writes one OBJ per class.
// Build/run: tools/geom/export.sh
using System;
using System.IO;
using System.Text;
using Submarines.Visual;
using UnityEngine;

public static class ExportObj
{
    public static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : ".";
        Write(Path.Combine(dir, "ssn_kestrel.obj"), HullGeometry.Build(HullPlan.Attack));
        Write(Path.Combine(dir, "ssbn_leviathan.obj"), HullGeometry.Build(HullPlan.Ballistic));
        return 0;
    }

    private static void Write(string path, MeshData m)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# generated from Submarines/src/Visual/HullGeometry.cs");
        // Unity is left-handed; flip X for right-handed OBJ viewers and reverse winding to match.
        foreach (Vector3 v in m.Verts)
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "v {0:F4} {1:F4} {2:F4}\n", -v.x, v.y, v.z);
        string[] names = { "hull", "detail" };
        for (int s = 0; s < 2; s++)
        {
            sb.AppendLine("g " + names[s]);
            sb.AppendLine("usemtl " + names[s]);
            var t = m.Tris[s];
            for (int i = 0; i + 2 < t.Count; i += 3)
                sb.AppendFormat("f {0} {1} {2}\n", t[i] + 1, t[i + 2] + 1, t[i + 1] + 1);
        }
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine(path + ": " + m.VertexCount + " verts, " + (m.Tris[0].Count + m.Tris[1].Count) / 3 + " tris");
    }
}
