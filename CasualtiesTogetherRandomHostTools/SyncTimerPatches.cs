using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class SyncTimerPatches
{
    public static float TilemapSyncTime = -1f;
    public static float FluidTilemapSyncTime = -1f;
    public static float ObjectFastSyncTime = -1f;
    public static float ObjectNormalSyncTime = -1f;
    public static float ObjectRareSyncTime = -1f;
    public static float TilemapSyncTimeDefault = -1f;
    public static float FluidTilemapSyncTimeDefault = -1f;
    public static float ObjectFastSyncTimeDefault = -1f;
    public static float ObjectNormalSyncTimeDefault = -1f;
    public static float ObjectRareSyncTimeDefault = -1f;

    private static float GetTilemapSyncTimer(float original)
        => TilemapSyncTime >= 0f ? TilemapSyncTime : TilemapSyncTimeDefault = original;
    
    private static float GetFluidSyncTimer(float original)
    {
        if (FluidTilemapSyncTime >= 0f)
            return FluidTilemapSyncTime;
        if (Plugin.IsKaizoEnabled && WorldGeneration.world.biomeDepth == 1)
            return 0.15f;
        return FluidTilemapSyncTimeDefault = original;
    }

    private static float GetFastObjectSyncTimer(float original)
        => ObjectFastSyncTime >= 0f ? ObjectFastSyncTime : ObjectFastSyncTimeDefault = original;

    private static float GetNormalObjectSyncTimer(float original)
        => ObjectNormalSyncTime >= 0f ? ObjectNormalSyncTime : ObjectNormalSyncTimeDefault = original;

    private static float GetRareObjectSyncTimer(float original)
        => ObjectRareSyncTime >= 0f ? ObjectRareSyncTime : ObjectRareSyncTimeDefault = original;
    
    [HarmonyPatch(typeof(WorldChunkSync), nameof(WorldChunkSync.LateUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> WorldChunkSyncTimersTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .End()
            .MatchBack(false,
                new CodeMatch(OpCodes.Ldfld,
                    AccessTools.Field(typeof(WorldChunkSync), nameof(WorldChunkSync.timer_TilemapFluidSync))))
            .ThrowIfInvalid($"{nameof(SyncTimerPatches)}.{nameof(WorldChunkSyncTimersTranspiler)} could not find a match (ldfld timer_TilemapFluidSync)!")
            .Advance(2)
            .ThrowIfNotMatch($"{nameof(SyncTimerPatches)}.{nameof(WorldChunkSyncTimersTranspiler)} expected a ble.un!", new CodeMatch(OpCodes.Ble_Un))
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SyncTimerPatches), nameof(GetFluidSyncTimer))))
            .MatchBack(false,
                new CodeMatch(OpCodes.Ldfld,
                    AccessTools.Field(typeof(WorldChunkSync), nameof(WorldChunkSync.timer_TilemapSync))))
            .ThrowIfInvalid($"{nameof(SyncTimerPatches)}.{nameof(WorldChunkSyncTimersTranspiler)} could not find a match (ldfld timer_TilemapSync)!")
            .Advance(2)
            .ThrowIfNotMatch($"{nameof(SyncTimerPatches)}.{nameof(WorldChunkSyncTimersTranspiler)} expected a ble.un!", new CodeMatch(OpCodes.Ble_Un))
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SyncTimerPatches), nameof(GetTilemapSyncTimer))))
            .InstructionEnumeration();
    }
    
    [HarmonyPatch(typeof(NetObjectRegistry), nameof(NetObjectRegistry.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> NetObjectRegistrySyncTimersTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .End()
            .MatchBack(false,
                new CodeMatch(OpCodes.Ldsfld,
                    AccessTools.Field(typeof(NetObjectRegistry), nameof(NetObjectRegistry.timer_raresync))))
            .ThrowIfInvalid($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} could not find a match (ldsfld timer_raresync)!")
            .Advance(2)
            .ThrowIfNotMatch($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} expected a ble.un after ldsfld timer_raresync!", new CodeMatch(OpCodes.Ble_Un))
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SyncTimerPatches), nameof(GetRareObjectSyncTimer))))
            .MatchBack(false,
                new CodeMatch(OpCodes.Ldsfld,
                    AccessTools.Field(typeof(NetObjectRegistry), nameof(NetObjectRegistry.timer_objsync))))
            .ThrowIfInvalid($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} could not find a match (ldsfld timer_objsync)!")
            .Advance(2)
            .ThrowIfNotMatch($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} expected a ble.un after ldsfld timer_objsync!", new CodeMatch(OpCodes.Ble_Un))
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SyncTimerPatches), nameof(GetNormalObjectSyncTimer))))
            .MatchBack(false,
                new CodeMatch(OpCodes.Ldsfld,
                    AccessTools.Field(typeof(NetObjectRegistry), nameof(NetObjectRegistry.timer_fastsync))))
            .ThrowIfInvalid($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} could not find a match (ldsfld timer_fastsync)!")
            .Advance(2)
            .ThrowIfNotMatch($"{nameof(SyncTimerPatches)}.{nameof(NetObjectRegistrySyncTimersTranspiler)} expected a ble.un after ldsfld timer_fastsync!", new CodeMatch(OpCodes.Ble_Un))
            .Insert(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SyncTimerPatches), nameof(GetFastObjectSyncTimer))))
            .InstructionEnumeration();
    }
}
