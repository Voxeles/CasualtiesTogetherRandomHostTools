using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Update))]
internal static class KaizoTurretPatch
{
    // private static float GetTurretRadius() => Plugin.IsKaizoEnabled ? 20f : 9f;

    // private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    // {
    //     return new CodeMatcher(instructions).MatchForward(false,
    //             new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(ExplosionParams), nameof(ExplosionParams.range))))
    //         .ThrowIfInvalid($"{nameof(KaizoTurretPatch)}.{nameof(Transpiler)} could not find a match!")
    //         .Advance(-1)
    //         .RemoveInstruction()
    //         .Insert(new CodeInstruction(OpCodes.Call,
    //             AccessTools.Method(typeof(KaizoTurretPatch), nameof(GetTurretRadius))))
    //         .InstructionEnumeration();
    // }
}