using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Together;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class CloneMsrTargetPatch
{
    private static bool Prepare() => MsrCompat.Loaded;

    private static MethodBase TargetMethod() => MsrCompat.TryGetTargetClientId;

    // Make clones share the host's client ID for skin sprites
    private static void Postfix(Body body, ref uint targetId, ref bool __result)
    {
        if (__result || !Net.IsServer || !CloneManager.IsClone(body))
            return;

        targetId = MsrCompat.LocalClientId;
        __result = true;
    }
}

[HarmonyPatch]
internal static class CloneMsrRelayPatch
{
    private static bool Prepare() => MsrCompat.Loaded;

    private static MethodBase TargetMethod() => MsrCompat.RelaySpritesFromServer;

    private static void Postfix(uint ownerClientId, bool replaceSet, List<KeyValuePair<string, byte[]>> sprites, uint excludeClientId)
    {
        if (!Net.IsServer || !CloneManager.Any || ownerClientId != MsrCompat.LocalClientId)
            return;

        foreach (var clone in CloneManager.ReadyClones())
            MsrCompat.RelaySprites(clone.PlayerId, replaceSet, sprites, excludeClientId);
    }

    // Called when a clone is ready
    internal static void SendHostSkin(HostClone clone)
    {
        if (!MsrCompat.Loaded)
            return;

        try
        {
            var sprites = MsrCompat.GetSprites(MsrCompat.LocalClientId);
            if (sprites.Count > 0)
                MsrCompat.RelaySprites(clone.PlayerId, true, sprites, uint.MaxValue);
            MsrCompat.QueueRefresh();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Could not send the host's skin for clone {clone.Npc}:\n{ex}");
        }
    }
}

[HarmonyPatch]
internal static class CloneMsrSyncPatch
{
    private static bool Prepare() => MsrCompat.Loaded;

    private static MethodBase TargetMethod() => MsrCompat.SyncAllSpritesToClient;

    // Add the clone IDs to the cache temporarily to make this method work
    private static void Prefix(out List<uint> __state)
    {
        __state = null;
        if (!Net.IsServer || !CloneManager.Any)
            return;

        uint hostId = MsrCompat.LocalClientId;
        __state = CloneManager.ReadyClones()
            .Select(c => (uint)c.PlayerId)
            .Where(id => MsrCompat.TryAlias(id, hostId))
            .ToList();
    }

    private static void Finalizer(List<uint> __state)
    {
        if (__state == null)
            return;

        foreach (uint id in __state)
            MsrCompat.RemoveAlias(id);
    }
}
