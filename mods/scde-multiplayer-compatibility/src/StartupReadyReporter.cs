using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace JiuyeAyan.SCDEMultiplayerCompatibility
{
    // Observe existing UI state only. Never instantiate MainViewModel or change game state.
    internal static class StartupReadyReporter
    {
        private static readonly string LaunchId = Environment.GetEnvironmentVariable("SCDEModManagerLaunchId");
        private static readonly FieldInfo ViewModel = typeof(CrusaderDE.MainViewModel).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        private static bool finished;

        internal static void Tick()
        {
            if (finished || String.IsNullOrEmpty(LaunchId)) return;
            try
            {
                var view = ViewModel == null ? null : ViewModel.GetValue(null) as CrusaderDE.MainViewModel;
                if (view == null || spriteLoader.instance == null || !spriteLoader.instance.spritesLoaded) return;
                if (!view.Show_Frontend_MainMenu && !view.Show_Frontend_Controls_Selection) return;
                finished = true;
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardInfo("SCDEMM_STARTUP_READY " + LaunchId);
                File.WriteAllText(Path.Combine(Paths.GameRootPath, "_scde_manager", "startup-ready.txt"), LaunchId);
            }
            catch (Exception error)
            {
                finished = true;
                SCDEMultiplayerCompatibilityPlugin.LogSettingsGuardWarning("Startup status unavailable: " + error.Message);
            }
        }
    }
}
