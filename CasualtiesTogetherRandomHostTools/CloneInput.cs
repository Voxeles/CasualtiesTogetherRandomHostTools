using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

internal static class CloneInput
{
    internal static bool Capturing;
    private static int _depth;
    private static bool _replaying;

    internal static int Frame;
    internal static Vector2 Move;
    internal static Vector2 LookOffset;
    internal static bool EndedJump;

    // PlayerCamera.HandleInput may not run before Body.Update
    // Hence allow for a frame of lag in the event the order is reversed
    // If it's larger than that, HandleInput isn't running and so no inputs to mirror
    internal static bool IsFresh => Time.frameCount - Frame <= 1;

    internal static void Reset()
    {
        Capturing = false;
        _depth = 0;
    }

    internal static bool BeginHostAction(Body body)
    {
        if (!Capturing
            || _replaying
            || !CloneManager.Any || !PlayerCamera.main || body != PlayerCamera.main.body)
            return false;

        // We need to track depth for actions that call other actions
        _depth++;
        return true;
    }

    internal static bool EndHostAction(bool began)
    {
        return began && --_depth == 0;
    }

    internal static void Replay(Action<HostClone> action)
    {
        _replaying = true;
        try
        {
            foreach (var clone in CloneManager.ReadyClones())
            {
                try
                {
                    action(clone);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Clone {clone.Npc} failed to mirror an action:\n{ex}");
                }
            }
        }
        finally
        {
            _replaying = false;
        }
    }
}

[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.HandleInput))]
internal static class CloneInputCapturePatch
{
    private static void Prefix()
    {
        CloneInput.Reset();
        CloneInput.Capturing = true;
    }

    private static void Postfix(PlayerCamera __instance)
    {
        CloneInput.Reset();

        var body = __instance.body;
        if (body == null)
            return;

        CloneInput.Frame = Time.frameCount;
        CloneInput.Move = body.moveDir;
        CloneInput.LookOffset = body.targetLookPos - body.transform.position;
        CloneInput.EndedJump = body.endedJump;
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.SwitchHands))]
internal static class CloneSwitchHandsPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.SwitchHands());
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.Ragdoll))]
internal static class CloneRagdollPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.Ragdoll());
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.Jump))]
internal static class CloneJumpPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.Jump());
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.UseItemInHand))]
internal static class CloneUseItemInHandPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.UseItemInHand());
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.ThrowItem))]
internal static class CloneThrowItemPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(float force, bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.Throw(force));
    }
}

[HarmonyPatch(typeof(Body), nameof(Body.DropItem), typeof(int))]
internal static class CloneDropItemPatch
{
    private static void Prefix(Body __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.Drop());
    }
}

[HarmonyPatch(typeof(PantSound), nameof(PantSound.Bark))]
internal static class CloneBarkPatch
{
    private static void Prefix(PantSound __instance, out bool __state)
    {
        __state = CloneInput.BeginHostAction(__instance.body);
    }

    private static void Postfix(bool __state)
    {
        if (CloneInput.EndHostAction(__state))
            CloneInput.Replay(clone => clone.Bark());
    }
}
