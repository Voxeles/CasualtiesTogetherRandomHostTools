using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class KaizoLayer4Patches
{
    [HarmonyPatch(typeof(SpiderHandler), nameof(SpiderHandler.Start))]
    [HarmonyPrefix]
    private static void AddSplitterToSalads(SpiderHandler __instance)
    {
        if (__instance is SpiderHandlerTBE && !__instance.gameObject.TryGetComponent<SaladSplitter>(out _))
            __instance.gameObject.AddComponent<SaladSplitter>();
    }
}

public class SaladSplitter : MonoBehaviour
{
    public static int MaximumSplitCount = 1;

    private int _split = 0;

    private void OnDestroy()
    {
        if (!Plugin.IsKaizoEnabled
            || WorldGeneration.world.doingRegen
            || !TryGetComponent<BuildingEntity>(out var buildingEntity)
            || buildingEntity.health > 0.5f
            || _split >= MaximumSplitCount)
            return;

        var original = (GameObject)Resources.Load(buildingEntity.id);
        var dir = Random.insideUnitCircle * 10f;

        Instantiate(original, transform.position + (Vector3)dir, transform.rotation)
            .AddComponent<SaladSplitter>()._split = _split + 1;
        Instantiate(original, transform.position - (Vector3)dir, transform.rotation)
            .AddComponent<SaladSplitter>()._split = _split + 1;
    }
}
