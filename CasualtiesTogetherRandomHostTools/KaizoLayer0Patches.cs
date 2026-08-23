using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

internal static class KaizoLayer0Patches
{
    [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.WorldGenerateStructures))]
    private static class CorruptWorldPatch
    {
        private static void Postfix(WorldGeneration __instance, ref IEnumerator __result)
        {
            if (!Plugin.IsKaizoEnabled || __instance.biomeDepth != 0)
                return;
            __result = WorldGenerateStructuresPatched(__instance, __result);
        }

        private static IEnumerator WorldGenerateStructuresPatched(WorldGeneration inst, IEnumerator original)
        {
            inst.SetLoadingText("kaizocorruptingworld");
            yield return null;
            yield return WorldCorrupt.DoCorrupt();
            inst.UpdateWorld();
            
            while (original.MoveNext())
                yield return original.Current;
        }
    }
    
    [HarmonyPatch]
    private static class SkipWorldUpdatePatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<WorldGenerateTerrain>d__"));
            return AccessTools.Method(target, "MoveNext");
        }
        
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcher = new CodeMatcher(instructions, generator);

            var doUpdateLabel = generator.DefineLabel();
            var skipUpdateLabel = generator.DefineLabel();

            codeMatcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_1),
                    new CodeMatch(OpCodes.Call,
                        AccessTools.Method(typeof(WorldGeneration), nameof(WorldGeneration.UpdateWorld))))
                .ThrowIfInvalid($"{nameof(SkipWorldUpdatePatch)}.{nameof(Transpiler)} could not find a match!");

            var oldLabels = codeMatcher.Labels;
            codeMatcher.Labels = [];

            codeMatcher.Insert(
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.Logger))),
                new CodeInstruction(OpCodes.Ldstr, "IN TRANSPILATION"),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ManualLogSource), nameof(ManualLogSource.LogInfo))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.IsKaizoEnabled))),
                new CodeInstruction(OpCodes.Brfalse, doUpdateLabel),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Ldfld,
                    AccessTools.Field(typeof(WorldGeneration), nameof(WorldGeneration.biomeDepth))),
                new CodeInstruction(OpCodes.Brtrue, doUpdateLabel),
                new CodeInstruction(OpCodes.Br, skipUpdateLabel));

            codeMatcher.Labels = oldLabels;
            codeMatcher.Advance(9).Labels.Add(doUpdateLabel);
            codeMatcher.Advance(2).Labels.Add(skipUpdateLabel);

            return codeMatcher.InstructionEnumeration();
        }
    }
}