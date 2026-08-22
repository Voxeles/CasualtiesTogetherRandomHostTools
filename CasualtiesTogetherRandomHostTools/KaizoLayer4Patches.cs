using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class KaizoLayer4Patches
{
    [HarmonyPatch]
    private static class RemoveSalads
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<WorldPlaceEntities>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static int GetLoopCount() => Plugin.IsKaizoEnabled ? 0 : 3;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            return codeMatcher
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldstr, "thornbackelder"))
                .ThrowIfInvalid($"{nameof(RemoveSalads)}.{nameof(Transpiler)} could not find a match (ldstr thornbackelder)!")
                .MatchForward(false, 
                    new CodeMatch(OpCodes.Ldc_I4_3), 
                    new CodeMatch(OpCodes.Blt))
                .ThrowIfInvalid($"{nameof(RemoveSalads)}.{nameof(Transpiler)} could not find a match (index < 3)!")
                .RemoveInstruction()
                .Insert(
                    new CodeInstruction(OpCodes.Call, 
                        AccessTools.Method(typeof(RemoveSalads), nameof(GetLoopCount))))
                .InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.PlaceCrystals))]
    [HarmonyPrefix]
    private static void PlaceSalads(WorldGeneration __instance)
    {
        if (!Plugin.IsKaizoEnabled || __instance.biomeDepth != 4)
            return;

        var yOffset1 = (__instance.halfHeight - 20f) / 4f;
        var xOffset1 = __instance.halfWidth / 3f;
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(0, yOffset1), 
            Quaternion.identity);
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(xOffset1, yOffset1), 
            Quaternion.identity);
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(-xOffset1, yOffset1), 
            Quaternion.identity);

        var yOffset2 = -__instance.halfHeight / 2f;
        var xOffset2 = __instance.halfWidth / 2f;
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(0, yOffset2), 
            Quaternion.identity);
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(xOffset2, yOffset2), 
            Quaternion.identity);
        Object.Instantiate(
            Resources.Load("thornbackelder"),
            new Vector2(-xOffset2, yOffset2), 
            Quaternion.identity);
    }
}