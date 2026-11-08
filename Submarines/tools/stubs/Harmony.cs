// STUB for offline type-checking only (tools/check.sh). Not the real API; never ship or reference.
using System; using System.Reflection; using System.Collections.Generic;
namespace HarmonyLib { public class Harmony{ public Harmony(string id){} public PatchClassProcessor CreateClassProcessor(Type t){return null;} public void UnpatchSelf(){}}
 public class PatchClassProcessor{ public List<MethodInfo> Patch(){return null;}}
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple=true)] public class HarmonyPatch:Attribute{ public HarmonyPatch(Type t,string m){} public HarmonyPatch(Type t,string m,Type[] a){}}
 public static class CollectionExtensions{ public static T[] AddToArray<T>(this T[] a,T item){return a;}}
 public static class AccessTools{ public static PropertyInfo Property(Type t,string n){return null;} public static FieldInfo Field(Type t,string n){return null;} public static MethodInfo Method(Type t,string n,Type[] p){return null;} public static MethodInfo Method(Type t,string n){return null;} public static Type[] GetTypesFromAssembly(Assembly a){return null;}}}
