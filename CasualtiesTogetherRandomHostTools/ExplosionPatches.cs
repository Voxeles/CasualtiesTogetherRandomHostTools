using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
public static class ExplosionPatches
{
    public static ExplosionParams ParamsOverride = null;
    private static bool _isInCreateExplosionPrefix = false;

    [HarmonyPatch(typeof(WorldGeneration_CreateExplosion_MultiplayerPatch))]
    private static class MultiplayerModPatch
    {
        [HarmonyPatch(nameof(WorldGeneration_CreateExplosion_MultiplayerPatch.Prefix))]
        [HarmonyPrefix]
        public static void Prefix(ref ExplosionParams param)
        {
            if (Net.is_client || ParamsOverride == null)
                return;
            ParamsOverride.position = param.position;
            param = ParamsOverride;
            _isInCreateExplosionPrefix = true;
        }
    }
    
    [HarmonyPatch(typeof(WorldGeneration))]
    private static class GamePatch
    {
        [HarmonyPatch(nameof(WorldGeneration.CreateExplosion))]
        [HarmonyPrefix]
        public static void Prefix(ref ExplosionParams param)
        {
            if (!_isInCreateExplosionPrefix)
                return;
            param = ParamsOverride;
        }
        
        [HarmonyPatch(nameof(WorldGeneration.CreateExplosion))]
        [HarmonyPostfix]
        public static void Postfix(ref ExplosionParams param)
        {
            _isInCreateExplosionPrefix = false;
        }
    }
}
