using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class CloneCatPatchLookupPatch
{
    private static bool Prepare() => CatPatchCompat.Loaded;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return CatPatchCompat.CanStartHug;
        yield return CatPatchCompat.Hug.VisualStart;
        yield return CatPatchCompat.Kiss.VisualStart;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
            AccessTools.Method(typeof(ScavPlayer), nameof(ScavPlayer.TryGetPlayerAndBodyFromClientId)),
            AccessTools.Method(typeof(CloneCatPatchLookupPatch), nameof(TryGetPlayerAndBody)));
    }

    private static bool TryGetPlayerAndBody(knetid clientId, out ScavPlayer plr, out Body body)
    {
        if (ScavPlayer.TryGetPlayerAndBodyFromClientId(clientId, out plr, out body))
            return true;

        if (!CloneManager.TryGetById(clientId, out var clone) || !clone.Ready)
            return false;

        plr = null; // Shouldn't matter
        body = clone.Body;
        return true;
    }
}

[HarmonyPatch]
internal static class CloneCatPatchAcceptPatch
{
    private static bool Prepare() => CatPatchCompat.Loaded;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return CatPatchCompat.Hug.SendPromptToClient;
        yield return CatPatchCompat.Kiss.SendPromptToClient;
    }

    // Clones always accept, those sluts...
    private static bool Prefix(object __instance, MethodBase __originalMethod, knetid destinationClientId, knetid requesterClientId)
    {
        if (!CloneManager.TryGetById(destinationClientId, out _))
            return true;

        var interaction = __originalMethod == CatPatchCompat.Hug.SendPromptToClient ? CatPatchCompat.Hug : CatPatchCompat.Kiss;
        interaction.Accept(__instance, destinationClientId, requesterClientId);
        return false;
    }
}

[HarmonyPatch]
internal static class CloneCatPatchHostRequestPatch
{
    // Same as HugTargetFinder
    private const float TargetRadius = 4.5f;

    private static bool Prepare() => CatPatchCompat.Loaded;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return CatPatchCompat.Hug.TrySendRequest;
        yield return CatPatchCompat.Kiss.TrySendRequest;
    }

    // This is for the host, as they'll not be able to hug clones otherwise
    private static bool Prefix(object __instance, MethodBase __originalMethod)
    {
        if (!Net.IsServer || !CloneManager.Any)
            return true;

        var player = ScavPlayer.LocalPlayer;
        var body = player ? player.body : null;
        if (!IsHuggable(body))
            return true;

        Vector2 pos = body.transform.position;
        var clone = FindNearestClone(pos, out float cloneDistance);
        if (clone == null || FindNearestPlayerDistance(player, pos) < cloneDistance)
            return true;

        var interaction = __originalMethod == CatPatchCompat.Hug.TrySendRequest ? CatPatchCompat.Hug : CatPatchCompat.Kiss;
        interaction.SendRequest(__instance, clone.PlayerId);
        return false;
    }

    // Same as HugValidation.IsBodyHuggable
    private static bool IsHuggable(Body body)
        => body != null && body.alive && body.conscious;

    private static HostClone FindNearestClone(Vector2 pos, out float distance)
    {
        HostClone nearest = null;
        distance = float.PositiveInfinity;
        foreach (var clone in CloneManager.ReadyClones())
        {
            if (!IsHuggable(clone.Body))
                continue;

            float d = Vector2.Distance(pos, clone.Body.transform.position);
            if (d <= TargetRadius && d < distance)
            {
                nearest = clone;
                distance = d;
            }
        }
        return nearest;
    }

    // Same as HugTargetFinder.FindNearestHuggableTarget
    private static float FindNearestPlayerDistance(ScavPlayer localPlayer, Vector2 pos)
    {
        float distance = float.PositiveInfinity;
        foreach (var player in ScavPlayer.GetPlayersInRadius(pos, TargetRadius))
        {
            if (!player || player == localPlayer || !IsHuggable(player.body))
                continue;

            distance = Mathf.Min(distance, Vector2.Distance(pos, player.body.transform.position));
        }
        return distance;
    }
}
