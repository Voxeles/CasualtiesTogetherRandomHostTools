using HarmonyLib;
using Together;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.ApplyLayerModifiers))]
internal static class KaizoApplyLayerModifiersPatch
{
    [HarmonyPriority(Priority.VeryLow)]
    private static bool Prefix()
    {
        if (Plugin.IsKaizoEnabled && Net.IsServer)
            return false; // Do not run, disable random layer modifiers when kaizo mode is on
        return true;
    }
}
