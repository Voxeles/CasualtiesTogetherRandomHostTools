using System.Collections.Generic;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using System.Reflection.Emit;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(KrokoshaSoundCannonNetworkTrackerComponent), nameof(KrokoshaSoundCannonNetworkTrackerComponent.Update))]
internal static class SoundCannonPatch
{
    private static float GetConsciousnessDecreaseWhenBlocked(Transform soundCannon, Transform body)
    {
        if (!Plugin.IsKaizoEnabled)
            return 30f;
        var dist = Mathf.Clamp(Vector2.Distance(soundCannon.position, body.position), 0f, 50f);
        return Mathf.Lerp(30f, 0f, dist / 50f);
    }

    private static void RemindPlayerOfTheirState(NetPlayer player)
    {
        if (!Plugin.IsKaizoEnabled || !player.TryGetNetBody(out var netBody))
            return;
        MedicalSync.Server_SendCharacterHealth(netBody);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(true,
                new CodeMatch(OpCodes.Ldfld,
                    AccessTools.Field(typeof(Body), nameof(Body.consciousness))),
                new CodeMatch(OpCodes.Ldc_R4, 30f))
            .ThrowIfInvalid($"{nameof(SoundCannonPatch)}.{nameof(Transpiler)} could not find match (ldfld Body.consciousness)!")
            .RemoveInstruction()
            .Insert(
                // var tmp1 = this.transform
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))),
                // var tmp2 = body.transform
                new CodeInstruction(OpCodes.Ldloc, 14),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))),
                // GetConsciousnessDecreaseWhenBlocked(tmp1, tmp2)
                new CodeInstruction(OpCodes.Call, 
                    AccessTools.Method(typeof(SoundCannonPatch), nameof(GetConsciousnessDecreaseWhenBlocked))))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    AccessTools.PropertyGetter(typeof(NetPlayer), nameof(NetPlayer.is_local))))
            .ThrowIfInvalid($"{nameof(SoundCannonPatch)}.{nameof(Transpiler)} could not find match (callvirt NetPlayer.get_is_local)!")
            .Insert(
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(SoundCannonPatch), nameof(RemindPlayerOfTheirState))))
            .InstructionEnumeration();
    }
}