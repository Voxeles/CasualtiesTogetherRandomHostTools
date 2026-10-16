using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesTogetherUtils;
using HarmonyLib;
using LiteNetLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(Body), nameof(Body.Attack))]
internal static class CloneMeleePatch
{
    internal sealed class AttackState
    {
        internal HostClone Clone;
        internal AttackInfo Attack;
        internal float Power;
        internal Vector2 From;
        internal Vector2 To;
        internal Item Tool;
    }

    // It's protected?
    private static readonly Func<object, object> ShallowCopy =
        AccessTools.MethodDelegate<Func<object, object>>(AccessTools.Method(typeof(object), "MemberwiseClone"));

    // Run after the MP mod's prefix
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Body __instance, AttackInfo atk, ref AttackState __state, bool __runOriginal)
    {
        if (!__runOriginal
            || !Net.IsServer
            || !CloneManager.TryGet(__instance, out var clone)
            || !__instance.conscious
            || __instance.attackCooldown > 0f)
        {
            return;
        }

        var hand = __instance.GetItem(__instance.handSlot);
        __state = new AttackState
        {
            Clone = clone,
            // Body.Attack modifies the attack info, keep the original values like the MP mod does
            Attack = (AttackInfo)ShallowCopy(atk),
            Power = Body_Attack_MultiplayerPatch.CalculateAttackPower(__instance, atk),
            From = __instance.limbs[1].transform.position,
            To = __instance.targetLookPos,
            Tool = Body_UseItem_MultiplayerPatch.current_used_item
                ?? (hand && hand.Stats.usable && CombatStuff.ItemIsUsedForAttacking(hand) ? hand : null)
        };
    }

    // Run before the MP mod's postfix, which clears the recorded hits
    [HarmonyPriority(Priority.First)]
    private static void Postfix(Body __instance, AttackState __state)
    {
        if (__state == null)
            return;

        try
        {
            ApplyHit(__instance, __state);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Clone melee hit failed:\n{ex}");
        }
        CloneMeleeCooldownPatch.Remember(__instance);

        bool hasTool = __state.Tool && NetObjectRegistry.TryGetSyncInfo(__state.Tool.gameObject, out _);
        HostClone.SendToClients(NetmsgId.CLIENT_PlayerBodyAttack_Relay, w =>
        {
            w.Put(__state.Clone.PlayerId);
            w.Put(hasTool);
            w.Put(hasTool ? NetObjectRegistry.GetSyncInfo(__state.Tool).syncId : 0);
            w.Put(__state.From);
            w.Put(__state.To);
        }, DeliveryMethod.Unreliable);
    }

    // Mirrors Body_Attack_MultiplayerPatch.ServerReceiver_PlayerBodyAttack
    private static void ApplyHit(Body attacker, AttackState state)
    {
        var limb = Body_Attack_MultiplayerPatch._LastHitLimb;
        var building = Body_Attack_MultiplayerPatch._LastHitBuilding;

        if (limb)
        {
            if (!limb.body.alive)
                limb = CombatStuff.CannibalismGetFarthestLimbToBeEfficient(limb);
            if (!limb.TryGetNetBody(out var target))
                return;

            float damage = Body_Attack_MultiplayerPatch.DoPVPMeleeDamage(attacker, limb, state.Attack, state.Power, state.Tool, out var knockback);
            var packet = new BodyKnockbackPacket { Velocity = knockback, CloseUI = true };
            if (target.IsPlayer)
            {
                var writer = Net.CreateWriter((byte)NetmsgId.CLIENT_Knockback_thing);
                writer.Put(packet);
                Net.Server_SendToClients(DeliveryMethod.Unreliable, writer, target.plr.playerId);
            }
            if (!target.IsLocal)
                Body_Attack_MultiplayerPatch.DoKnockback(limb.body, new BodyKnockbackPacket { Velocity = knockback });
            WorldGeneration.CreateDamageNumber(target.GetHeadPos() + Vector2.up, (int)damage);
            MedicalSync.Server_QueueSendCharacterHealth(target, true);
        }
        else if (building)
        {
            float damage = Body_Attack_MultiplayerPatch.DamageBuilding(attacker, state.Attack, building, state.Power, true);
            WorldGeneration.CreateDamageNumber((Vector2)building.transform.position + Vector2.up * 2f, (int)damage);
        }
    }
}

// The MP mod shortens the attack cooldown of non-local bodies for latency reasons?
// Do not do that to clones
[HarmonyPatch(typeof(Body), nameof(Body.Attack))]
internal static class CloneMeleeCooldownPatch
{
    private static Body _body;
    private static float _cooldown;

    // Called after the attack (and its hit) set the cooldown, but before the MP mod's postfix shortens it
    internal static void Remember(Body body)
    {
        _body = body;
        _cooldown = body.attackCooldown;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Body __instance)
    {
        if (_body is null || _body != __instance)
            return;

        __instance.attackCooldown = _cooldown;
        _body = null;
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.Update))]
internal static class CloneCooldownDecayPatch
{
    // Undo Body_Update_MultiplayerPatch's faster cooldown decay for non-local bodies
    private static void Postfix(Body __instance)
    {
        if (Net.IsRunning && __instance.alive && CloneManager.IsClone(__instance))
            __instance.attackCooldown += Time.deltaTime * 0.05f;
    }
}

[HarmonyPatch(typeof(GunScript_Fire_MultiplayerPatch), nameof(GunScript_Fire_MultiplayerPatch._GunFirePostfix_CheckForHitsAndTellServer))]
internal static class CloneGunfirePatch
{
    // For non-local bodies the MP mod only checks for static hits and expects the shooter's client to report the rest
    // For clones, do the full hit check and apply the damage like ServerReceiver__PlayerBodyShoot would
    private static bool Prefix(GunScript gun, bool suicide)
    {
        if (!Net.IsServer || !CloneManager.Any)
            return true;

        if (!gun.TryGetComponent<TrackerGunScript>(out var tracker))
            return true;
        tracker.ScanForBody();
        if (!CloneManager.TryGet(tracker.body, out var clone))
            return true;

        try
        {
            Fire(gun, tracker, clone, suicide);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Clone gunfire failed:\n{ex}");
        }
        finally
        {
            TurretScript_Shoot_MultiplayerPatch.HitOnlyStatic = false;
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooter = null;
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooterGun = null;
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooterIsSuicide = false;
            tracker.override_shoot_trajectory = false;
        }
        return false;
    }

    private static void Fire(GunScript gun, TrackerGunScript tracker, HostClone clone, bool suicide)
    {
        var body = clone.Body;
        float facing = body.isRight ? 1f : -1f;
        Vector2 origin = gun.barrel.position;
        Vector2 direction = gun.transform.right * facing;

        var limbs = new List<Limb>();
        var buildings = new List<SyncInfo>();
        TurretScript_Shoot_MultiplayerPatch.HitOnlyStatic = false;
        for (int i = 0; i < tracker.shotsPerFire; i++)
        {
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooterIsSuicide = suicide;
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooter = clone.Npc;
            TurretScript_Shoot_MultiplayerPatch.LastActiveShooterGun = tracker;
            TurretScript.Shoot(new FireInfo
            {
                pos = origin,
                dir = suicide
                    ? KM.NormalTo(origin, body.limbs[0].rb.position)
                    : ((Vector2)gun.transform.right + GunScript_Fire_MultiplayerPatch.RollVerticalSpread(gun)) * facing,
                doTinnitus = false,
                ignoreBody = false,
                structureDamage = gun.structureDamage,
                animalDamage = gun.animalDamage,
                forceHead = false
            });
            limbs.AddRange(TurretScript_Shoot_MultiplayerPatch.JustHitLimbs);
            buildings.AddRange(TurretScript_Shoot_MultiplayerPatch.JustHitBuildings);
        }
        tracker.send_racked_state = true;

        var hitLimbs = Util.LimitMaxRepeats(limbs, Math.Min(tracker.shotsPerFire, GunScript_Fire_MultiplayerPatch.MAX_HITS_PER_LIMB_AT_ONCE))
            .Where(l => l.body.TryGetNetBody(out _))
            .ToList();
        var hitBuildings = Util.LimitMaxRepeats(buildings, tracker.shotsPerFire);
        TurretScript_Shoot_MultiplayerPatch.ApplyShootDamages(tracker, hitLimbs, hitBuildings);
        if (suicide && !hitLimbs.Contains(clone.Npc.head))
            TurretScript_Shoot_MultiplayerPatch.Stolen_ShootApplyDamageLimb(clone.Npc.head, tracker, false);

        if (suicide || tracker.si == null)
            return;
        HostClone.SendToClients(NetmsgId.CLIENT_PlayerBodyShoot_Relay, w =>
        {
            w.Put(clone.PlayerId);
            w.Put(tracker.tracker.syncId);
            w.Put(origin);
            w.Put(direction);
        });
    }
}

[HarmonyPatch(typeof(TurretScript_Shoot_MultiplayerPatch), nameof(TurretScript_Shoot_MultiplayerPatch.Shoot_DidHitABody))]
internal static class CloneGunTargetPatch
{
    // Returning true means the MP mod handles the hit itself and the vanilla damage is skipped
    private static void Postfix(ref RaycastHit2D hit2D, ref bool __result)
    {
        if (__result || !Net.IsServer || !CloneManager.Any)
            return;

        var shooter = TurretScript_Shoot_MultiplayerPatch.LastActiveShooter;
        if (!shooter)
            return;

        var body = hit2D.collider.TryGetComponent<Limb>(out var limb) ? limb.body : hit2D.collider.GetComponent<Body>();
        if (!body || !CloneManager.IsClone(body))
            return;

        // Suicide shots are handled separately
        if (TurretScript_Shoot_MultiplayerPatch.LastActiveShooterIsSuicide && shooter.body == body)
            return;

        __result = true;
    }
}

[HarmonyPatch(typeof(NetBody), nameof(NetBody.CanAttackThisGuy))]
internal static class CloneCanAttackPatch
{
    // Clones never hurt the host. They also don't have a player for the MP mod's team check, so handle teams here
    private static void Postfix(NetBody __instance, Body target, ref bool __result)
    {
        if (!__result || !CloneManager.Any || !target.alive)
            return;

        bool attackerIsClone = CloneManager.TryGet(__instance.body, out var attackerClone);
        bool targetIsClone = CloneManager.TryGet(target, out var targetClone);
        if (!attackerIsClone && !targetIsClone)
            return;

        if (attackerIsClone && Util.IsBodyLocal(target))
        {
            __result = false;
            return;
        }

        if (!Multiplayer.rules.Teams)
            return;

        Color24? attackerColor = attackerIsClone ? attackerClone.Color : __instance.IsPlayer ? __instance.plr.playerColor : null;
        Color24? targetColor = null;
        if (targetIsClone)
            targetColor = targetClone.Color;
        else if (target.TryGetNetBody(out var targetNb) && targetNb.IsPlayer)
            targetColor = targetNb.plr.playerColor;

        if (attackerColor.HasValue && targetColor.HasValue && attackerColor.Value == targetColor.Value)
            __result = false;
    }
}
