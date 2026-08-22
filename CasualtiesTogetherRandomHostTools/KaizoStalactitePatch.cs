using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class StalactiteApplyExploderPatch
{
    [HarmonyPatch(typeof(StalactiteDropper), nameof(StalactiteDropper.Start))]
    [HarmonyPostfix]
    private static void StartPostfix(StalactiteDropper __instance)
    {
        if (!__instance.build.id.Equals("stalactite"))
            return;
        // Check for IsKaizoEnabled in the exploder itself, to allow for hot-applying KaizoEnabled for testing
        __instance.gameObject.AddComponent<StalactiteExploder>();
        if (Plugin.IsKaizoEnabled)
            __instance.countTime = 0f;
    }

    [HarmonyPatch(typeof(StalactiteDropper), nameof(StalactiteDropper.OnCollisionEnter2D))]
    [HarmonyPostfix]
    private static void OnCollisionEnter2DPostfix(StalactiteDropper __instance, Collision2D collision)
    {
        if (Plugin.IsKaizoEnabled
            && __instance.dropped
            && Mathf.Abs(collision.relativeVelocity.y) > 10f
            && __instance.build.id.Equals("stalactite")
            && !collision.gameObject.TryGetComponent<Item>(out _))
        {
            __instance.build.health = 0f;
        }
    }
}

internal class StalactiteExploder : MonoBehaviour
{
    private void OnDestroy()
    {
        if (!Plugin.IsKaizoEnabled 
            || WorldGeneration.world.doingRegen
            || !TryGetComponent<BuildingEntity>(out var buildingEntity)
            || buildingEntity.health > 0.5f)
            return;
        
        GetComponent<Collider2D>().enabled = false;
        WorldGeneration.CreateExplosion(new ExplosionParams
        {
            position = transform.position,
            range = 5f,
            velocity = 25f,
            structuralDamage = 200f,
            shrapnelChance = 0.1f,
            skinDamageChance = 0.1f,
            skinDamage = new RangeF(0.0f, 10f),
            bleedChance = 0.1f,
            bleedAmount = new RangeF(0.0f, 15f),
            muscleDamage = new RangeF(0.0f, 10f),
            dislocationChance = 0.0f,
            boneBreakChance = 0.0f,
            disfigureChance = 0f
        });
        
        if (Net.is_client)
            return;

        foreach (var netBody in NetBody.GetBodiesInRadius(transform.position, 5f))
            netBody.Server_RemindPlayersCurrentState(keep_velocity: true, reliable: true);
    }
}
