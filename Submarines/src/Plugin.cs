using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Submarines
{
    internal static class PluginInfo
    {
        public const string Guid = "com.badnew21.submarines";
        public const string Name = "Submarines";
        public const string Version = "0.5.0";
    }

    /// <summary>
    /// Adds two submarine classes (SSN attack sub, SSBN ballistic missile sub), an underwater
    /// acoustic model with passive/active sonar, scripted heavyweight torpedoes and a sonar
    /// spoofing suite (noisemaker decoys, false-echo repeaters, acoustic mimicry).
    /// </summary>
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency(Bootstrap.BoteBridge.BoteGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SubmarinesPlugin : BaseUnityPlugin
    {
        internal static SubmarinesPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            SubConfig.Bind(Config);
            try
            {
                _harmony = new Harmony(PluginInfo.Guid);
                // Patch class by class so one renamed game method only disables that feature.
                int ok = 0, failed = 0;
                foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(SubmarinesPlugin).Assembly))
                {
                    if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                        continue;
                    try
                    {
                        _harmony.CreateClassProcessor(t).Patch();
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log.LogError("Patch " + t.Name + " failed (feature disabled): " + ex.Message);
                    }
                }
                Log.LogInfo(PluginInfo.Name + " " + PluginInfo.Version + " loaded (" + ok + " patches, " + failed + " failed).");
                Bootstrap.BoteBridge.Hook();
            }
            catch (Exception ex)
            {
                Log.LogError("Harmony patching failed: " + ex);
            }
        }

        private void Update()
        {
            Sync.SubNet.Tick();
            Runtime.UnderwaterWorld.Tick();
            Visual.TorpedoVisuals.Tick();
            UI.SonarDisplay.TickInput();
        }

        private void OnGUI()
        {
            UI.SonarDisplay.Draw();
        }

        private void OnDestroy()
        {
            try { if (_harmony != null) _harmony.UnpatchSelf(); }
            catch { }
            Instance = null;
        }
    }
}
