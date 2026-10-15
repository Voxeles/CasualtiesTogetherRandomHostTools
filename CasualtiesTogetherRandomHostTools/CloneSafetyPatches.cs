using CasualtiesTogetherUtils;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(NetBody), nameof(NetBody.GetLastDamageEvent))]
internal static class CloneLastDamageEventPatch
{
    // Dereferences plr without checking for null
    private static bool Prefix(NetBody __instance, ref BodyDamageEvent __result)
    {
        if (__instance.IsPlayer)
            return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(ItemSync), nameof(ItemSync.CheckIfBodyReachThisItem), typeof(GameObject), typeof(Body), typeof(float), typeof(bool))]
internal static class CloneReachItemPatch
{
    // The obstruction check dereferences the player of the body holding the item
    // Run the logic for the playerless clone
    private static bool Prefix(GameObject item, Body my_body, float maxdist, bool check_obstruction, ref bool __result)
    {
        if (!check_obstruction || !CloneManager.Any)
            return true;

        var info = ItemSync.ItemGetContainerInfo(item);
        if (!info.is_netbody || info.plr || !info.bodynpc || !CloneManager.IsClone(info.bodynpc.body))
            return true;

        var holder = item.GetComponentInParent<NetBody>();
        if (holder && holder.body == my_body)
        {
            __result = true;
            return false;
        }

        __result = ItemSync.CheckIfBodyCanStealThisItem(item, my_body, info)
                   && KM.DistanceCheck(item.transform.position, my_body.transform.position, maxdist)
                   && Util.AccurateRaycastInteractionCheckObstruction(my_body, info.bodynpc.body, false);
        return false;
    }
}
