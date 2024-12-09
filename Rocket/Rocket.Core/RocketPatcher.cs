using System.Reflection;
using HarmonyLib;

namespace Rocket.Core;

internal static class RocketPatcher
{
    public static readonly Harmony Harmony;
    
    static RocketPatcher()
    {
        Harmony = new(typeof(RocketPatcher).FullName);
    }
    
    public static void PatchRocket()
    {
        Harmony.PatchAll(typeof(Rocket.Core.AssemblyMarker).Assembly);
        Harmony.PatchAll(typeof(Rocket.Common.AssemblyMarker).Assembly);
    }
    
    public static void Patch(Assembly assembly)
    {
        Harmony.PatchAll(assembly);
    }

    public static void Unpatch()
    {
        Harmony.UnpatchAll(Harmony.Id);
    }
}