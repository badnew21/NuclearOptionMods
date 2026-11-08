// STUB for offline type-checking only (tools/check.sh). Not the real API; never ship or reference.
using System;
namespace BepInEx { public class BaseUnityPlugin:UnityEngine.MonoBehaviour{ protected BepInEx.Logging.ManualLogSource Logger; public BepInEx.Configuration.ConfigFile Config;}
 [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin:Attribute{ public BepInPlugin(string a,string b,string c){}}
 [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)] public class BepInDependency:Attribute{ public enum DependencyFlags{HardDependency=1,SoftDependency=2} public BepInDependency(string g,DependencyFlags f){}}}
namespace BepInEx.Logging { public class ManualLogSource{ public void LogInfo(object o){} public void LogWarning(object o){} public void LogError(object o){} public void LogDebug(object o){}}}
namespace BepInEx.Configuration { public class ConfigFile{ public ConfigEntry<T> Bind<T>(string s,string k,T d,string desc){return null;}} public class ConfigEntry<T>{ public T Value;}}
