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
internal static class FinalDestinationPatch
{
    public static bool ShouldDoTheOverride = false;

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.GenerateWorld))]
    [HarmonyPrefix]
    private static void OverrideBiomePatch()
    {
        if (ShouldDoTheOverride)
            WorldGeneration.world.biomeOverride = WorldGeneration.OverrideSceneType.Debug;
    }
    
    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateTerrain))]
    private static class GeneratePlatformPatch
    {
        [HarmonyPriority(Priority.VeryLow)]
        private static bool Prefix(WorldGeneration __instance, ref IEnumerator __result)
        {
            if (!ShouldDoTheOverride)
                return true;
            __result = WorldGenerateTerrainPatched(__instance);
            return false;
        }

        private static IEnumerator WorldGenerateTerrainPatched(WorldGeneration inst)
        {
            inst.SetLoadingText("gencreatingterrain");
            yield return null;
            var center = inst.WorldToBlockPos(new(0, -2));
            inst.DrawLine(center + new Vector2Int(-40, 0), center + new Vector2Int(40, 0), 3, 14);
            inst.DrawLine(center + new Vector2Int(-37, -3), center + new Vector2Int(37, -3), 2, 14);
            inst.DrawLine(center + new Vector2Int(-30, -5), center + new Vector2Int(30, -5), 1, 14);
            inst.DrawLine(center + new Vector2Int(-20, -6), center + new Vector2Int(20, -6), 1, 14);
            inst.DrawLine(center + new Vector2Int(-10, -7), center + new Vector2Int(10, -7), 1, 14);
        }
    }

    private static IEnumerator Dummy()
    {
        yield break;
    }
    
    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateWorldBorders))]
    [HarmonyPrefix]
    private static bool SkipGenerateWorldBordersPatch(ref IEnumerator __result)
    {
        if (!ShouldDoTheOverride)
            return true;
        __result = Dummy();
        return false;
    }

    [HarmonyPatch]
    private static class FinishOverridePatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<GenerateWorld>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static void OnEnd()
        {
            if (!ShouldDoTheOverride) 
                return;
            ShouldDoTheOverride = false;
            WorldgenPatches.CreateSkyBackground(Color.white, 2f);
            WorldGeneration.world.gameObject.AddComponent<KillPlayersAtBottomAndHideSavePanel>();
        }
        
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .End()
                .Insert(
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(FinishOverridePatch), nameof(OnEnd))))
                .InstructionEnumeration();
        }
    }
    
    [HarmonyPatch(typeof(WorldgenPatches), nameof(WorldgenPatches.ServerReceiver_FinishedWorldgen))]
    [HarmonyPostfix]
    private static void QueueTileSyncForPlayer(knetid clientId)
    {
        if (!Util.IsInWorld()
            || !GameObject.FindAnyObjectByType<KillPlayersAtBottomAndHideSavePanel>()
            || !NetPlayer.TryGetPlayerFromClientId(clientId, out var plr)
            || plr.is_local)
            return;

        plr.StartCoroutine(SyncBottomHalfTiles(clientId));
    }
    
    private static IEnumerator SyncBottomHalfTiles(knetid clientId)
    {
        var inst = WorldGeneration.world;
        
        var timer = 0f;
        var endChunkX = inst.width / 32;
        var endChunkY = inst.height / 32 / 2;
        var coord = new Vector2UInt8();
        for (byte chunkX = 0; chunkX < endChunkX; chunkX++)
        {
            for (byte chunkY = 0; chunkY < endChunkY; chunkY++)
            {
                coord.x = chunkX;
                coord.y = chunkY;
                WorldChunkSync.Server_Sendchunk(coord, clientId);

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

public class KillPlayersAtBottomAndHideSavePanel : MonoBehaviour
{
    private float _timer = 0f;
    
    private void Update()
    {
        if (WorldGeneration.world.doingRegen)
        {
            Destroy(this);
            return;
        }
        
        if (WorldGeneration.world.savePanel.activeSelf)
            WorldGeneration.world.savePanel.SetActive(false);

        _timer += Time.deltaTime;
        if (_timer < 1.75f)
            return;
        _timer = 0f;
        
        foreach (var plr in NetPlayer.ClientIdToPlayerDict.Values)
        {
            if (plr.body == null
                || !plr.body.rb.simulated 
                || plr.body.GetPosition().y > -WorldGeneration.world.halfHeight + 3f
                || !plr.body.alive)
                continue;
            plr.body.brainHealth = 0.0f;
            plr.body.heartRate = 0.0f;
        }
    }
}