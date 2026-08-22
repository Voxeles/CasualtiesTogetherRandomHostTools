using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
public class KaizoDynamitePatch
{
    private static bool _isInDynamiteExplode = false;
    
    // private static float GetDynamiteRadius() => Plugin.IsKaizoEnabled ? 25f : 18f;

    [HarmonyPatch(typeof(CustomItemBehaviour), nameof(CustomItemBehaviour.DynamiteExplode))]
    private static void Prefix() => _isInDynamiteExplode = true;

    [HarmonyPatch(typeof(CustomItemBehaviour), nameof(CustomItemBehaviour.DynamiteExplode))]
    private static void Postfix() => _isInDynamiteExplode = false;

    // [HarmonyPatch(typeof(CustomItemBehaviour), nameof(CustomItemBehaviour.DynamiteExplode))]
    // [HarmonyTranspiler]
    // private static IEnumerable<CodeInstruction> ExplosionRadiusTranspiler(IEnumerable<CodeInstruction> instructions)
    // {
    //     return new CodeMatcher(instructions)
    //         .MatchForward(false, 
    //             new CodeMatch(OpCodes.Stfld, 
    //                 AccessTools.Field(typeof(ExplosionParams), nameof(ExplosionParams.range))))
    //         .ThrowIfInvalid($"{nameof(KaizoDynamitePatch)}.{nameof(ExplosionRadiusTranspiler)} could not find a match!")
    //         .Advance(-1)
    //         .RemoveInstruction()
    //         .Insert(
    //             new CodeInstruction(OpCodes.Call, 
    //                 AccessTools.Method(typeof(KaizoDynamitePatch), nameof(GetDynamiteRadius))))
    //         .InstructionEnumeration();
    // }

    private static float GetExtraDamage(BuildingEntity buildingEntity)
    {
        if (Plugin.IsKaizoEnabled && _isInDynamiteExplode && buildingEntity.id.Equals("thornbackelder"))
            return 1000f;
        return 0f;
    }

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.CreateExplosion))]
    private static IEnumerable<CodeInstruction> ExtraDamageTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, 
                new CodeMatch(OpCodes.Stfld, 
                    AccessTools.Field(typeof(BuildingEntity), nameof(BuildingEntity.health))))
            .ThrowIfInvalid($"{nameof(KaizoDynamitePatch)}.{nameof(ExtraDamageTranspiler)} could not find a match!")
            .Insert(
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(KaizoDynamitePatch), nameof(GetExtraDamage))),
                new CodeInstruction(OpCodes.Sub))
            .InstructionEnumeration();
    }
}