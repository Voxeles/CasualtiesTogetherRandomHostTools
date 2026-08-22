using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using LiteNetLib;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class KaizoLayer1Patches
{
    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateWorldBorders))]
    private static class FloodWorldPatch
    {
        [HarmonyPriority(Priority.VeryLow)]
        private static bool Prefix(WorldGeneration __instance, ref IEnumerator __result)
        {
            if (!Plugin.IsKaizoEnabled)
                return true;
            __result = WorldGenerateWorldBordersPatched(__instance, __result);
            return false;
        }
    
        private static IEnumerator WorldGenerateWorldBordersPatched(WorldGeneration inst, IEnumerator original)
        {
            yield return original;
            if (!Plugin.IsKaizoEnabled || inst.biomeDepth != 1) 
                yield break;
            
            inst.SetLoadingText("kaizofloodingworld");
            yield return null;
            var floodedModifier = LayerModifier.availableModifiers[5];
            floodedModifier.Initialize(inst);
            floodedModifier.active = true;
            inst.layerPrefix = Locale.GetOther("layermodifier" + floodedModifier.modifierIndex);
            inst.layerDescription = Locale.GetOther($"layermodifier{floodedModifier.modifierIndex.ToString()}dsc");
            WorldCorrupt.ReplaceAllLiquids(6);
        }
    }

    [HarmonyPatch]
    private static class RemoveGeysersPatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<WorldPlaceEntities>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Ldstr, "geyser"))
                .ThrowIfInvalid($"{nameof(RemoveGeysersPatch)}.{nameof(Transpiler)} could not find a match (pos before call)!");
            
            var beforeCallPos = codeMatcher.Pos;

            codeMatcher.MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    AccessTools.Method(typeof(WorldGeneration), nameof(WorldGeneration.DistributeEntities))))
                .ThrowIfInvalid($"{nameof(RemoveGeysersPatch)}.{nameof(Transpiler)} could not find a match (pos after call)!")
                .Advance(1);
            
            if (codeMatcher.Instruction.opcode != OpCodes.Br)
                throw new InvalidOperationException("Expected a branch!");

            var label = codeMatcher.Operand;

            codeMatcher
                .Advance(beforeCallPos - codeMatcher.Pos)
                .Insert(
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.IsKaizoEnabled))),
                    new CodeInstruction(OpCodes.Brtrue, label));
            
            return codeMatcher.InstructionEnumeration();
        }
    }

    private static float GetSyncTimer(float original)
        => Plugin.IsKaizoEnabled && WorldGeneration.world.biomeDepth == 1 ? 0.15f : original;

    [HarmonyPatch(typeof(WorldChunkSync), nameof(WorldChunkSync.LateUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SyncFasterTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .End()
            .MatchBack(false,
                new CodeMatch(OpCodes.Ble_Un))
            .ThrowIfInvalid($"{nameof(KaizoLayer1Patches)}.{nameof(SyncFasterTranspiler)} could not find a match!")
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(KaizoLayer1Patches), nameof(GetSyncTimer))))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(WorldgenPatches), nameof(WorldgenPatches.ServerReceiver_FinishedWorldgen))]
    [HarmonyPostfix]
    private static void QueueLiquidSyncForPlayer(knetid clientId)
    {
        if (!Plugin.IsKaizoEnabled
            || !Util.IsInWorld()
            || WorldGeneration.world.biomeDepth != 1
            || !NetPlayer.TryGetPlayerFromClientId(clientId, out var plr)
            || plr.is_local)
            return;

        plr.StartCoroutine(SyncAllLiquidChunks(clientId));
    }

    private static IEnumerator SyncAllLiquidChunks(knetid clientId)
    {
        var inst = WorldGeneration.world;
        
        var timer = 0f;
        var endChunkX = (byte)(inst.width / 32);
        var startChunkY = (byte)(inst.height / 32 - 1);
        var coord = new Vector2UInt8();
        for (byte chunkX = 0; chunkX < endChunkX; chunkX++)
        {
            for (int chunkY = startChunkY; chunkY >= 0; chunkY--)
            {
                coord.x = chunkX;
                coord.y = (byte)chunkY;
                var writer = WorldChunkSync._Server_PackFluidChunk(in coord);
                Net.Server_SendToClients(DeliveryMethod.Unreliable, in writer, clientId);

                while (timer < 0.02f)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
                timer = 0f;
            }
        }
    }
}