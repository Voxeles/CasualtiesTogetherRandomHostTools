using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiteEntitySystem;
using Together;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesTogetherRandomHostTools;

public static class CloneManager
{
    private static readonly Dictionary<Body, HostClone> Clones = new();

    public static bool Any
        => Clones.Count > 0;

    public static bool IsRemoving { get; private set; }

    public static bool TryGet(Body body, out HostClone clone)
    {
        clone = null;
        return Clones.Count > 0 && body is not null && Clones.TryGetValue(body, out clone) && clone;
    }

    public static bool IsClone(Body body)
        => TryGet(body, out _);

    public static bool IsCloneId(byte id)
        => Clones.Values.Any(c => c && c.Owner != null && c.Owner.Id == id);

    public static IEnumerable<NetPlayer> Owners
        => Clones.Values.Where(c => c && c.Owner != null).Select(c => c.Owner);

    public static HostClone[] ReadyClones()
        => Clones.Values.Where(c => c && c.Ready).ToArray();

    public static HostClone Spawn(NetBody host, Vector2 pos)
    {
        var manager = Net.ServerEntityManager;
        var hostPlayer = ScavPlayer.LocalPlayer;
        if (manager == null || !hostPlayer)
            throw new Exception("Server isn't running!");

        var initialState = SavedPlayerState.Create(host);
        var name = hostPlayer.playerName;
        var color = hostPlayer.playerColor;

        var owner = CloneLesPatches.AddPlayer(manager, new ClonePeer());
        if (owner == null)
            throw new Exception("No free player ids left for a clone!");

        try
        {
            var basePlayer = manager.AddEntity<BasePlayer>(e =>
            {
                e.playerSteamId.Value = hostPlayer.SteamId; // Needs to be a real steam ID that is in a lobby
                e.playerName.Value = name;
                e.playerColor.Value = color;
                e.IsWindowFocused.Value = true;
            });
            manager.AddController<BasePlayerController>(owner, basePlayer);

            var syncBody = manager.AddEntity<SyncBody>(e =>
            {
                e._position.Value = pos;
                e.SetParent(basePlayer);
            });
            if (syncBody.nb == null)
                throw new Exception("SyncBody has no NetBody!");

            var npc = syncBody.nb;
            npc.chara.name = $"CLONE_ID_{syncBody.Id}";
            npc.ApplyNameAndColor(name, color);
            npc.SetBodyPosition(pos);

            var clone = npc.gameObject.AddComponent<HostClone>();
            clone.Initialize(npc, manager, owner, color, initialState, host.body.handSlot);
            Clones[npc.body] = clone;
            return clone;
        }
        catch
        {
            CloneLesPatches.RemovePlayer(manager, owner);
            throw;
        }
    }

    public static void Remove(HostClone clone)
    {
        if (clone == null)
            return;

        Forget(clone);

        var npc = clone.Npc;
        if (npc == null)
            return;

        foreach (var item in npc.body.GetAllItemsThorough().ToArray())
            Object.Destroy(item.gameObject);
        NetBody.DestroyNPC(npc);
    }

    public static int RemoveAll()
    {
        var clones = Clones.Values.Where(c => c != null).ToArray();
        if (clones.Length == 0)
            return 0;

        foreach (var clone in clones)
            clone.Ready = false;

        // Destroying a body is slow as shit. 80ms cold on my pc
        // Spread the destruction over several frames
        Plugin.Instance.StartCoroutine(RemoveAllCoro(clones));
        return clones.Length;
    }

    private static IEnumerator RemoveAllCoro(HostClone[] clones)
    {
        IsRemoving = true;
        var manager = Net.ServerEntityManager;
        try
        {
            foreach (var clone in clones)
            {
                if (manager == null || Net.ServerEntityManager != manager)
                    yield break;

                if (clone == null)
                    continue;

                // Remove and yield for destroy
                Remove(clone);
                yield return null;

                ushort removedTick = manager.Tick;
                float deadline = Time.realtimeSinceStartup + 1.5f;

                // Wait a bit for the clients to catch up to prevent batching
                while (Time.realtimeSinceStartup < deadline
                       && Net.ServerEntityManager == manager
                       && !AllClientsReached(manager, removedTick))
                {
                    yield return null;
                }
            }
        }
        finally
        {
            IsRemoving = false;
        }
    }

    private static bool AllClientsReached(ServerEntityManager manager, ushort tick)
    {
        for (int i = 0; i < manager._netPlayers.Count; i++)
        {
            var player = manager._netPlayers.GetByIndex(i);
            if (player.State != NetPlayerState.Active)
                continue;
            if (LiteEntitySystem.Utils.SequenceDiff(player.StateATick, tick) < 0)
                return false;
        }
        return true;
    }

    public static void Forget(HostClone clone)
    {
        foreach (var pair in Clones.Where(pair => pair.Value == clone).ToArray())
            Clones.Remove(pair.Key);
    }

    public static void ApplyCloneStateAll(NetBody sourceBody)
    {
        if (!sourceBody)
            return;

        var state = SavedPlayerState.Create(sourceBody);

        foreach (var clone in Clones.Values.Where(c => c != null).ToArray())
        {
            try
            {
                state.Apply(clone.Npc, SavedPlayerState.RestoreSelection.Health | SavedPlayerState.RestoreSelection.Inventory);
                clone.SetHandSlot(sourceBody.body.handSlot);
            }
            catch (Exception ex)
            {
                Plugin.PrintWarning("Failed to update clone: " + ex);
            }
        }
    }
}
