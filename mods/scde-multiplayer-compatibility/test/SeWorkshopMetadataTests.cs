using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using JiuyeAyan.SCDEMultiplayerCompatibility;

internal static class SeWorkshopMetadataTests
{
    private static int Main(string[] args)
    {
        Assembly.Load("System.Text.Json"); // Desktop test host; live probe separately checks SE's Unity-loaded version.
        SeWorkshopMetadata.Initialize(Assembly.LoadFrom(args[0]));
        foreach (string name in new[] { "plugin.map", "asset.map", "ordinary.map", "missing.map", "invalid.map", "large.map", "quoted.map" })
        {
            string file = Path.Combine(args[1], name);
            bool expected = name == "plugin.map";
            if (SeWorkshopMetadata.IsPluginMap(file) != expected) throw new Exception("Unexpected classification: " + name);
            using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        var timer = Stopwatch.StartNew();
        if (args.Length > 2 && !SeWorkshopMetadata.IsPluginMap(args[2])) throw new Exception("Subscribed Serps map not recognized");
        Console.WriteLine("SE_WORKSHOP_METADATA_OK cases=7 handlesReleased=true realPackageMs=" + timer.Elapsed.TotalMilliseconds);
        return 0;
    }
}
