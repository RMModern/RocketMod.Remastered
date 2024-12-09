using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace Rocket.Common;

[Harmony]
internal static class HotloaderPatcher
{
    /// <summary>
    /// Patch to retrieve real assembly name by default.
    /// </summary>
    [HarmonyPatch(typeof(Assembly), nameof(GetName), [ ])]
    [HarmonyPrefix]
    public static bool GetName(Assembly __instance, ref AssemblyName __result)
    {
        var stackTrace = new StackTrace();

        bool ShouldIgnoreFrame(StackFrame stackFrame) => stackFrame.GetMethod().DeclaringType == typeof(Hotloader);
        
        if (ShouldIgnoreFrame(stackTrace.GetFrame(1)) || 
            ShouldIgnoreFrame(stackTrace.GetFrame(2)))
            return true;

        __result = Hotloader.GetRealAssemblyName(__instance);
        
        return false;
    }
}