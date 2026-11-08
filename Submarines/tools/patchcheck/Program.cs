// Usage: PatchCheck <NuclearOptionRoot> <path/to/Submarines.dll>
//
// Loads the mod and the game's assemblies as metadata only (nothing runs) and checks each
// [HarmonyPatch(type, "method"[, argTypes])] the way Harmony will at startup:
//   - the method exists on the type or a base type,
//   - it's unambiguous (or the argument types pick exactly one overload),
//   - every injected parameter of Prefix/Postfix (other than __instance/__result/__state) names a
//     real parameter of the target method.
// Any failure means that patch would be skipped in game, so the build fails.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: PatchCheck <NuclearOptionRoot> <Submarines.dll>");
            return 2;
        }
        string root = args[0];
        string modPath = Path.GetFullPath(args[1]);
        var paths = new List<string>();
        paths.AddRange(Directory.GetFiles(Path.Combine(root, "NuclearOption_Data", "Managed"), "*.dll"));
        paths.AddRange(Directory.GetFiles(Path.Combine(root, "BepInEx", "core"), "*.dll"));
        paths.Add(modPath);

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
        Assembly mod = mlc.LoadFromAssemblyPath(modPath);

        int checkedCount = 0, failures = 0;
        foreach (Type patchClass in mod.GetTypes())
        {
            foreach (CustomAttributeData attr in patchClass.GetCustomAttributesData())
            {
                if (attr.AttributeType.FullName != "HarmonyLib.HarmonyPatch")
                    continue;
                checkedCount++;
                string problem = Check(patchClass, attr, out string label);
                if (problem == null)
                {
                    Console.WriteLine("ok    " + label);
                }
                else
                {
                    failures++;
                    Console.WriteLine("FAIL  " + label + ": " + problem);
                    Console.WriteLine("::error::Harmony patch " + patchClass.Name + " -> " + label + ": " + problem);
                }
            }
        }
        Console.WriteLine(checkedCount + " patch target(s) checked, " + failures + " failure(s).");
        return failures == 0 && checkedCount > 0 ? 0 : 1;
    }

    private static string Check(Type patchClass, CustomAttributeData attr, out string label)
    {
        var a = attr.ConstructorArguments;
        Type target = a.Count > 0 ? a[0].Value as Type : null;
        string name = a.Count > 1 ? a[1].Value as string : null;
        Type[] argTypes = null;
        if (a.Count > 2 && a[2].Value is IReadOnlyCollection<CustomAttributeTypedArgument> arr)
            argTypes = arr.Select(x => (Type)x.Value).ToArray();
        label = (target != null ? target.FullName : "?") + "." + name +
                (argTypes != null ? "(" + string.Join(", ", argTypes.Select(t => t.Name)) + ")" : "");
        if (target == null || name == null)
            return "unsupported attribute form";

        // Harmony (AccessTools.Method) takes the first type up the hierarchy that declares the name.
        MethodInfo[] candidates = null;
        for (Type t = target; t != null; t = t.BaseType)
        {
            MethodInfo[] declared = t.GetMethods(Declared).Where(m => m.Name == name).ToArray();
            if (declared.Length > 0)
            {
                candidates = declared;
                break;
            }
        }
        if (candidates == null)
            return "no method named '" + name + "'";

        MethodInfo method;
        if (argTypes != null)
        {
            method = candidates.FirstOrDefault(m =>
            {
                ParameterInfo[] ps = m.GetParameters();
                return ps.Length == argTypes.Length &&
                       ps.Select(p => p.ParameterType.FullName).SequenceEqual(argTypes.Select(t => t.FullName));
            });
            if (method == null)
                return "no overload matches; available: " + Overloads(candidates);
        }
        else
        {
            if (candidates.Length > 1)
                return "ambiguous, " + candidates.Length + " overloads: " + Overloads(candidates) + " (add argument types)";
            method = candidates[0];
        }

        var targetParams = new HashSet<string>(method.GetParameters().Select(p => p.Name));
        foreach (MethodInfo hook in patchClass.GetMethods(Declared))
        {
            if (hook.Name != "Prefix" && hook.Name != "Postfix")
                continue;
            foreach (ParameterInfo p in hook.GetParameters())
            {
                if (p.Name.StartsWith("__", StringComparison.Ordinal))
                    continue;
                if (!targetParams.Contains(p.Name))
                    return hook.Name + " parameter '" + p.Name + "' is not a parameter of the target (has: " +
                           string.Join(", ", targetParams) + ")";
            }
        }
        return null;
    }

    private static string Overloads(IEnumerable<MethodInfo> ms)
    {
        return string.Join(" | ", ms.Select(m => m.Name + "(" + string.Join(", ",
            m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"));
    }
}
