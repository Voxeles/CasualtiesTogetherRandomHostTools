using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
internal static class KaizoLayer3Patches
{
    private static bool _isWithinWorldPlaceEntities = false;
    
    [HarmonyPatch]
    private static class ReplaceAllTrapsWithSoundCannonsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(Resources))
                .Single(m => m.Name == nameof(Resources.Load)
                             && !m.IsGenericMethod
                             && m.GetParameters().Length == 1
                             && m.GetParameters()[0].ParameterType == typeof(string));
        }

        [HarmonyPriority(Priority.VeryLow)]
        private static void Prefix(ref string path)
        {
            if (!Plugin.IsKaizoEnabled || !_isWithinWorldPlaceEntities || WorldGeneration.world.biomeDepth != 3)
                return;
            
            path = path switch
            {
                "jumppad" or "landmine" or "spentfuel" or "coil" or "oilpipe" or "turret" or "stalactite" => "soundcannon",
                _ => path
            };
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateStructures))]
    [HarmonyPrefix]
    private static void GenerateSafeLinePatch(WorldGeneration __instance)
    {
        if (!Plugin.IsKaizoEnabled || WorldGeneration.world.biomeDepth != 3)
            return;
        
        uint width = __instance.width;
        uint y = __instance.height - 16;
        for (uint i = 0; i < width; i++)
        {
            __instance.worldBlocks[i, y + 6] = 0;
            __instance.worldBlocks[i, y + 5] = 0;
            __instance.worldBlocks[i, y + 4] = 0;
            __instance.worldBlocks[i, y + 3] = 0;
            __instance.worldBlocks[i, y + 2] = 0;
            __instance.worldBlocks[i, y + 1] = 0;
            __instance.worldBlocks[i, y] = 1;
        }
        __instance.UpdateWorld();
    }
    
    [HarmonyPatch]
    private static class DestroySoundCannonsAboveSafeLinePatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<GenerateWorld>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static void OnEnd()
        {
            if (!Plugin.IsKaizoEnabled || WorldGeneration.world.biomeDepth != 3)
                return;
            
            var maxY = WorldGeneration.world.halfHeight - 16f;
        
            foreach (var obj in GameObject.FindObjectsByType<SoundCannon>(FindObjectsSortMode.None))
            {
                if (obj == null || obj.transform.position.y < maxY)
                    continue;
                obj.GetComponent<BuildingEntity>().health = 0f;
                GameObject.Destroy(obj.gameObject);
            }
        }
        
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .End()
                .Insert(
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(DestroySoundCannonsAboveSafeLinePatch), nameof(OnEnd))))
                .InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.ApplyLayerModifiers))]
    [HarmonyPrefix]
    private static void ApplyUnchippedModifierPatch(WorldGeneration __instance)
    {
        if (!Plugin.IsKaizoEnabled || WorldGeneration.world.biomeDepth != 3)
            return;
        
        var unchippedModifier = LayerModifier.availableModifiers[10];
        unchippedModifier.Initialize(__instance);
        unchippedModifier.active = true;
        __instance.layerPrefix = Locale.GetOther("layermodifier" + unchippedModifier.modifierIndex);
        __instance.layerDescription = Locale.GetOther($"layermodifier{unchippedModifier.modifierIndex.ToString()}dsc");
    }

    // Stalactites are expected to have the StalactiteDropper component, which sound cannons do not have.
    // We need to skip the StalactiteDropper setup.
    // Also add prefix and postfix for _isWithinWorldPlaceEntities
    [HarmonyPatch]
    private static class FixTrapReplacerPatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<WorldPlaceEntities>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static void Prefix() => _isWithinWorldPlaceEntities = true;
        
        private static void Postfix() => _isWithinWorldPlaceEntities = false;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcher = new CodeMatcher(instructions, generator);

            var doStalactiteDropperSetupLabel = generator.DefineLabel();
            var skipStalactiteDropperSetupLabel = generator.DefineLabel();

            codeMatcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldfld,
                        AccessTools.Field(typeof(WorldGeneration), nameof(WorldGeneration.biomeDepth))),
                    new CodeMatch(OpCodes.Ldc_I4_3))
                .ThrowIfInvalid($"{nameof(FixTrapReplacerPatch)}.{nameof(Transpiler)} could not find a match (biomeDepth check)!")
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldstr, "stalactite"))
                .ThrowIfInvalid($"{nameof(FixTrapReplacerPatch)}.{nameof(Transpiler)} could not find a match (stalactite ldstr)!")
                .MatchForward(true,
                    new CodeMatch(OpCodes.Castclass, typeof(GameObject)),
                    new CodeMatch(OpCodes.Stloc_S))
                .ThrowIfInvalid($"{nameof(FixTrapReplacerPatch)}.{nameof(Transpiler)} could not find a match (store GameObject)!")
                .Advance(1);
            
            codeMatcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.IsKaizoEnabled))),
                new CodeInstruction(OpCodes.Brfalse, doStalactiteDropperSetupLabel),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Ldfld,
                    AccessTools.Field(typeof(WorldGeneration), nameof(WorldGeneration.biomeDepth))),
                new CodeInstruction(OpCodes.Ldc_I4_3),
                new CodeInstruction(OpCodes.Bne_Un, doStalactiteDropperSetupLabel),
                new CodeInstruction(OpCodes.Br, skipStalactiteDropperSetupLabel));
            
            codeMatcher.Labels.Add(doStalactiteDropperSetupLabel);

            codeMatcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt,
                        AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localScale))))
                .ThrowIfInvalid($"{nameof(FixTrapReplacerPatch)}.{nameof(Transpiler)} could not find a match (set localScale)!")
                .Advance(1)
                .Labels.Add(skipStalactiteDropperSetupLabel);
            
            return codeMatcher.InstructionEnumeration();
        }
    }
}
