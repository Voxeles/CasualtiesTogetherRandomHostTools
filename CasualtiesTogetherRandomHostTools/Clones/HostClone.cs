using System;
using System.Collections;
using LiteEntitySystem;
using LiteNetLib;
using LiteNetLib.Utils;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public sealed class HostClone : MonoBehaviour
{
    public NetBody Npc;
    public NetPlayer Owner;
    public Color24 Color;
    public CoUtils CoUtils;
    public bool Ready;

    private ServerEntityManager _manager;
    private SavedPlayerState _initialState;
    private int _initialHandSlot;

    internal Body Body => Npc.body;

    internal ushort PlayerId => Owner.Id;

    internal void Initialize(NetBody npc, ServerEntityManager manager, NetPlayer owner, Color24 color, SavedPlayerState initialState, int handSlot)
    {
        Npc = npc;
        _manager = manager;
        Owner = owner;
        Color = color;
        _initialState = initialState;
        _initialHandSlot = handSlot;

        CoUtils = new GameObject("CloneCoUtils").AddComponent<CoUtils>();
        CoUtils.transform.SetParent(npc.chara.transform);
    }

    private IEnumerator Start()
    {
        // Let the body finish its own setup before overwriting its state
        yield return null;

        if (!Net.IsRunning || !Net.IsServer)
            yield break;

        try
        {
            _initialState.Apply(Npc, SavedPlayerState.RestoreSelection.Health | SavedPlayerState.RestoreSelection.Inventory);
            SetHandSlot(_initialHandSlot);
            Ready = true;
            CloneMsrRelayPatch.SendHostSkin(this);
        }
        catch (Exception ex)
        {
            Plugin.PrintError($"Could not initialize clone {Npc}!\n{ex}");
            CloneManager.Remove(this);
        }
        finally
        {
            _initialState = null;
        }
    }

    private void OnDestroy()
    {
        CloneManager.Forget(this);
        if (Owner != null && Net.ServerEntityManager == _manager && _manager != null)
            CloneLesPatches.RemovePlayer(_manager, Owner);
        Owner = null;
    }

    internal void SwitchHands()
    {
        if (!Body.conscious)
            return;
        Body.SwitchHands();
        SendHandSlot();
    }

    internal void Ragdoll()
    {
        Body.Ragdoll();
    }

    internal void Jump()
    {
        if (!Body.conscious)
            return;
        Body.Jump();
        Body.endedJump = false;
    }

    internal void Throw(float force)
    {
        if (!Body.conscious || !Body.HoldingItem(Body.handSlot))
            return;
        Body.ThrowItem(force);
    }

    internal void Drop()
    {
        if (!Body.conscious)
            return;
        Body.DropItem(Body.handSlot);
    }

    internal void Bark()
    {
        if (!Body.conscious)
            return;
        Body.GetComponent<PantSound>().Bark();
        // Bark() sets eatTime to exactly 0.5 if it played the bark
        if (Body.eatTime == 0.5f)
            SendToClients(NetmsgId.CLIENT_PlayerBarkRelay, w => w.Put(PlayerId));
    }

    internal void UseItemInHand()
    {
        if (!Body.conscious)
            return;

        var item = Body.GetItem(Body.handSlot);
        var previous = CoUtils_instance_MultiplayerPatch.cur_override_instance;
        try
        {
            CoUtils_instance_MultiplayerPatch.cur_override_instance = CoUtils;
            Body.UseItemInHand();
        }
        finally
        {
            CoUtils_instance_MultiplayerPatch.cur_override_instance = previous;
        }

        if (item
            && item.Stats.usable
            && item.Stats.usableWithLMB
            && !CombatStuff.ItemIsUsedForAttacking(item)
            && NetObjectRegistry.TryGetSyncInfo(item.gameObject, out var si))
        {
            SendToClients(NetmsgId.CLIENT_RequestUseItemRelay, w =>
            {
                w.Put(PlayerId);
                w.Put(si.syncId);
                w.Put(Npc.netId);
            });
        }
    }

    internal void SetHandSlot(int slot)
    {
        Body.handSlot = slot;
        SendHandSlot();
    }

    private void SendHandSlot()
    {
        SendToClients(NetmsgId.CLIENT_PlayerCamera_SwitchHandsRelay, w =>
        {
            w.Put(PlayerId);
            w.Put((byte)Body.handSlot);
        });
    }

    internal static void SendToClients(NetmsgId id, Action<NetDataWriter> write, DeliveryMethod delivery = DeliveryMethod.ReliableUnordered)
    {
        var writer = Net.CreateWriter((byte)id);
        write(writer);
        Net.Server_SendToClients(delivery, writer, ServerMain.AllClientIdsExceptHost);
    }
}
