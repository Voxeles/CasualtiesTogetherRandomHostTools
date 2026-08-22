using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.ApplyLayerModifiers))]
internal static class KaizoApplyLayerModifiersPatch
{
    [HarmonyPriority(Priority.VeryLow)]
    private static bool Prefix()
    {
        if (Plugin.IsKaizoEnabled && Net.is_server)
            return false; // Do not run, disable random layer modifiers when kaizo mode is on
        return true;
    }
}