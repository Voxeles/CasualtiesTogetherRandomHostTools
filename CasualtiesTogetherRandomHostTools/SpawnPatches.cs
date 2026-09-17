using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
public static class SpawnPatches
{
    private static bool _savedX = false;
    private static float _x;

    private static int GetStartingDepthForSpawnHijack()
    {
        var world = WorldGeneration.world;

        if (Net.is_client || world.biomeOverride != WorldGeneration.OverrideSceneType.None)
            return 0;

        var depth = GetStartingDepthSetting();

        try
        {
            if (WorldGeneration.GetRunSettingBool(RunSettingsPatches.ClearSpawnLocationKey))
            {
                float worldX = _savedX ? _x : 0f;
                ClearSpawnLocation(new Vector2Int((int)(worldX + world.halfWidth), (int)world.height - depth));
            }
        }
        catch (Exception ex)
        {
            Plugin.PrintWarning($"Exception thrown while clearing the clear spawn location setting:\n\t{ex.Message}\n{ex.StackTrace}");
        }

        return depth;
    }

    private static void ClearSpawnLocation(Vector2Int blockPos)
    {
        const int minX = -4, maxX = 3, minY = -8, maxY = 2;

        var world = WorldGeneration.world;
        var fluid = FluidManager.main;

        var blockPosCopyShutUpRoslyn = blockPos;
        var kaizoAlwaysAddWalls = Plugin.IsKaizoEnabled && (WorldGeneration.world.biomeDepth == 3 && blockPosCopyShutUpRoslyn.y < world.height - 16);
        var kaizoAlwaysAddRoof = kaizoAlwaysAddWalls || (Plugin.IsKaizoEnabled && WorldGeneration.world.biomeDepth == 2);

        foreach (var ent in GameObject.FindObjectsByType<BuildingEntity>(FindObjectsSortMode.None))
        {
            var l = world.WorldToBlockPos(ent.transform.position) - blockPos;
            if (l.x is >= minX and <= maxX && l.y is >= minY and <= maxY)
                GameObject.Destroy(ent.gameObject);
        }

        {
            var leftWallX = blockPosCopyShutUpRoslyn.x + minX - 1;
            var addLeftWall = false;
            addLeftWall |= kaizoAlwaysAddWalls;
            for (int y = minY; y <= maxY; ++y)
                addLeftWall |= fluid.GetLiquid(leftWallX, blockPosCopyShutUpRoslyn.y + y) != 0;

            if (addLeftWall)
            {
                for (int y = minY; y <= maxY; ++y)
                    world.SetBlockNoUpdate(blockPos + new Vector2Int(minX, y), 1);
            }
        }
        {
            var rightWallX = blockPosCopyShutUpRoslyn.x + maxX + 1;
            var addRightWall = false;
            addRightWall |= kaizoAlwaysAddWalls;
            for (int y = minY; y <= maxY; ++y)
                addRightWall |= fluid.GetLiquid(rightWallX, blockPosCopyShutUpRoslyn.y + y) != 0;

            if (addRightWall)
            {
                for (int y = minY; y <= maxY; ++y)
                    world.SetBlockNoUpdate(blockPos + new Vector2Int(maxX, y), 1);
            }
        }
        {
            var roofY = blockPosCopyShutUpRoslyn.y + maxY + 1;
            var addRoof = false;
            addRoof |= kaizoAlwaysAddRoof;
            for (int x = minX; x <= maxX; ++x)
                addRoof |= fluid.GetLiquid(blockPosCopyShutUpRoslyn.x + x, roofY) != 0;

            if (addRoof)
            {
                for (int x = minX; x <= maxX; ++x)
                    world.SetBlockNoUpdate(blockPos + new Vector2Int(x, maxY), 1);
            }
        }

        for (int x = minX + 1; x <= maxX - 1; ++x)
        {
            world.SetBlockNoUpdate(blockPos + new Vector2Int(x, minY), 1);
            for (int y = minY + 1; y <= maxY - 1; ++y)
                world.SetBlockNoUpdate(blockPos + new Vector2Int(x, y), 0);
        }

        for (int x = minX; x <= maxX; ++x)
        {
            for (int y = minY; y <= maxY; ++y)
            {
                var offset = blockPos + new Vector2Int(x, y);
                fluid.SetLiquid(offset.x, offset.y, 0);
            }
        }

        var processedChunks = new List<Vector2Int>(4);
        var offsets = new Vector2Int[]
        {
            new(minX, minY), new(minX, maxY), new(maxX, minY), new(maxX, maxY)
        };
        foreach (var offset in offsets)
        {
            var chunk = world.BlockToChunkPos(ClampBlockPos(blockPos + offset));
            if (processedChunks.Contains(chunk))
                continue;
            world.UpdateChunk(chunk);
            world.chunks[chunk.x, chunk.y].gameObject.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            processedChunks.Add(chunk);
        }

        return;

        static Vector2Int ClampBlockPos(Vector2Int pos)
        {
            var world = WorldGeneration.world;
            return new Vector2Int((int) Mathf.Clamp(pos.x, 0.0f, (world.width - 1U)), (int) Mathf.Clamp(pos.y, 0.0f, (world.height - 1U)));
        }
    }

    [HarmonyPatch(typeof(Body), nameof(Body.PlaceBody))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PlaceBodyTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector2), nameof(Vector2.up))))
            .ThrowIfInvalid($"{nameof(SpawnPatches)}.{nameof(PlaceBodyTranspiler)} could not find a match (Vector2.up)")
            .MatchBack(false, new CodeMatch(OpCodes.Ldc_I4_0))
            .ThrowIfInvalid($"{nameof(SpawnPatches)}.{nameof(PlaceBodyTranspiler)} could not find a match (ldc.i4.0)")
            .RemoveInstruction()
            .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SpawnPatches), nameof(GetStartingDepthForSpawnHijack))))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(Util), nameof(Util.PlaceBody_FindSpawnLocation))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FindSpawnLocationTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Br))
            .ThrowIfInvalid($"{nameof(SpawnPatches)}.{nameof(FindSpawnLocationTranspiler)} could not find a match (br)")
            .MatchBack(false, new CodeMatch(OpCodes.Ldc_I4_0))
            .ThrowIfInvalid($"{nameof(SpawnPatches)}.{nameof(FindSpawnLocationTranspiler)} could not find a match (ldc.i4.0)")
            .RemoveInstruction()
            .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SpawnPatches), nameof(GetStartingDepthForSpawnHijack))))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(Util), nameof(Util.PlaceBody_FindSpawnLocation))]
    [HarmonyPrefix]
    private static void LogPlayerXOffset(float x)
    {
        _x = x;
        _savedX = true;
    }

    [HarmonyPatch(typeof(Util), nameof(Util.PlaceBody_FindSpawnLocation))]
    [HarmonyPostfix]
    private static void ClearPlayerXOffset(float x)
    {
        _x = 0f;
        _savedX = false;
    }

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateWorldBorders))]
    [HarmonyPrefix]
    private static void GenerateBlockLinePatch(WorldGeneration __instance)
    {
        if (Net.is_client || __instance.biomeOverride != WorldGeneration.OverrideSceneType.None)
            return;

        try
        {
            if (!WorldGeneration.GetRunSettingBool(RunSettingsPatches.GenerateBlockLineKey))
                return;
        }
        catch (Exception ex)
        {
            Plugin.PrintWarning($"Exception thrown while reading the generate block line setting:\n\t{ex.Message}\n{ex.StackTrace}");
            return;
        }

        var depth = GetStartingDepthSetting();
        if (depth <= 16)
            return;

        uint width = __instance.width;
        uint y = __instance.height - (uint)depth - 4 + 16;
        for (uint i = 0; i < width; i++)
        {
            __instance.worldBlocks[i, y + 3] = 14;
            __instance.worldBlocks[i, y + 2] = 14;
            __instance.worldBlocks[i, y + 1] = 14;
            __instance.worldBlocks[i, y] = 14;
        }
        __instance.UpdateWorld();
    }

    private static int GetStartingDepthSetting()
    {
        var depth = 0;
        try
        {
            depth = (int)WorldGeneration.GetRunSettingFloat(RunSettingsPatches.StartingDepthKey);
            if (depth < 0)
                throw new Exception($"Invalid starting depth \"{depth}\" - Cannot be negative");
            if (depth > RunSettingsPatches.StartingDepthMaxValue)
                throw new Exception($"Invalid starting depth \"{depth}\" - Cannot be greater than {RunSettingsPatches.StartingDepthMaxValue}");
            // Ensure some buffer at the end no matter what - otherwise bad things can happen and I cannot be bothered
            if (WorldGeneration.world != null && depth > WorldGeneration.world.height - 24)
                throw new Exception($"Invalid starting depth \"{depth}\" - Cannot be greater than the world height of {WorldGeneration.world.height} - 24");
        }
        catch (Exception ex)
        {
            Plugin.PrintWarning($"Exception thrown while reading \"{RunSettingsPatches.StartingDepthKey}\", using default 0 instead.\n\t{ex.Message}\n{ex.StackTrace}");
        }

        return depth;
    }
}
