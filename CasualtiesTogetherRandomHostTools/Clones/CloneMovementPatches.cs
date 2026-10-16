using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class CloneMovementPatches
{
    [HarmonyPatch(typeof(Body), nameof(Body.Update))]
    [HarmonyPrefix]
    private static void CloneBodyInputPatch(Body __instance)
    {
        if (!CloneManager.TryGet(__instance, out var clone) || !clone.Ready)
            return;

        if (!CloneInput.IsFresh || !__instance.conscious)
        {
            __instance.moveDir = Vector2.zero;
            __instance.crouching = false;
            return;
        }

        __instance.moveDir = CloneInput.Move;
        __instance.crouching = __instance.reversedControls ? CloneInput.Move.y > 0 : CloneInput.Move.y < 0;
        __instance.targetLookPos = __instance.transform.position + (Vector3)CloneInput.LookOffset;
        __instance.endedJump = CloneInput.EndedJump;
    }

    [HarmonyPatch(typeof(NetBody), nameof(NetBody.FixedUpdate))]
    [HarmonyPrefix]
    private static void CloneSyncTimeoutPatch(NetBody __instance)
    {
        // NetBody.FixedUpdate zeroes moveDir of non-local bodies that haven't received a client packet recently
        if (Net.IsServer && CloneManager.IsClone(__instance.body))
            __instance._lastSyncPacketReceiveTick = Net.ServerTick;
    }

    [HarmonyPatch(typeof(SyncBody), nameof(SyncBody.Update))]
    [HarmonyPostfix]
    private static void CloneSyncCloneBodiesPatch(SyncBody __instance)
    {
        // The server only writes the sync vars for the host's own body, do it for clones as well
        if (!Net.IsServer || !__instance.nb || !CloneManager.IsClone(__instance.nb.body))
            return;

        __instance.WriteBodyData();
        // Sync this as well
        __instance._moveDir.Value = (SByteScalarVec2)__instance.nb.body.moveDir;
    }

    [HarmonyPatch(typeof(SyncBody), nameof(SyncBody.VisualUpdate))]
    [HarmonyPrefix]
    private static bool SkipApplyForCloneBodiesPatch(SyncBody __instance)
    {
        // Would apply stale data. Sync for thee but not for me
        return !(Net.IsServer && __instance.nb && CloneManager.IsClone(__instance.nb.body));
    }
}
