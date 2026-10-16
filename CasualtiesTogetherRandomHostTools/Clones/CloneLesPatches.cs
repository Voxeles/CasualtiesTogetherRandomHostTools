using HarmonyLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using Together;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(ServerEntityManager), nameof(ServerEntityManager.OnLogicTick))]
internal static class CloneLesPatches
{
    // Give clones the highest player IDs first. Hopefully they'll be unused
    private const byte HighestCloneId = 253;

    internal static NetPlayer AddPlayer(ServerEntityManager manager, AbstractNetPeer peer)
    {
        byte id = HighestCloneId;
        while (manager._netPlayers.Contains(id))
        {
            if (--id == 0)
                return null;
        }

        var player = new NetPlayer(peer, id, ServerEntityManager.MaxStoredInputs);
        manager._netPlayers.Set(id, player);
        peer.AssignedPlayer = player;
        Neutralize(manager, player);
        return player;
    }

    // This is just ServerEntityManager.RemovePlayer, but with the Id enqueue removed
    internal static void RemovePlayer(ServerEntityManager manager, NetPlayer player)
    {
        if (player == null || !manager._netPlayers.TryGetValue(player.Id, out var current) || current != player)
            return;

        manager.GetPlayerController(player)?.DestroyWithControlledEntity();
        manager._netPlayers.Remove(player.Id);
        player.State = NetPlayerState.Removed;
        player.RemoveAllRpcs(manager._rpcPool);
        manager._firstBaselineRequestedPlayers.Remove(player);
    }

    internal static void Neutralize(ServerEntityManager manager, NetPlayer player)
    {
        if (player.State == NetPlayerState.Removed)
            return;

        manager._firstBaselineRequestedPlayers.Remove(player);
        player.State = NetPlayerState.WaitingForFirstInput;
        player.FirstBaselineSent = false;
        player.RemoveAllRpcs(manager._rpcPool);
    }

    private static void NeutralizeAll(ServerEntityManager manager)
    {
        if (!CloneManager.Any)
            return;

        foreach (var owner in CloneManager.Owners)
            Neutralize(manager, owner);
    }

    // Remove all RPCs for the dummy players every tick
    private static void Prefix(ServerEntityManager __instance) => NeutralizeAll(__instance);
    private static void Postfix(ServerEntityManager __instance) => NeutralizeAll(__instance);
}

[HarmonyPatch(typeof(IdGeneratorByte), nameof(IdGeneratorByte.GetNewId))]
internal static class CloneReservedIdPatch
{
    // Skip IDs used by clones
    private static void Postfix(IdGeneratorByte __instance, ref byte __result)
    {
        if (!CloneManager.Any || Net.ServerEntityManager == null || __instance != Net.ServerEntityManager._playerIdQueue)
            return;

        while (CloneManager.IsCloneId(__result))
            __result = __instance.GetNewId();
    }
}
