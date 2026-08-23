using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

internal static class KaizoLayer2Patches
{
    [HarmonyPatch]
    private static class RemoveOilPipesPatch
    {
        private static MethodBase TargetMethod()
        {
            var target = AccessTools.FirstInner(typeof(WorldGeneration), t => t.Name.Contains("<WorldPlaceEntities>d__"));
            return AccessTools.Method(target, "MoveNext");
        }

        private static uint GetMultiplier() => Plugin.IsKaizoEnabled && WorldGeneration.world.biomeDepth == 2 ? 0u : 1u;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> DoNotSpawnOilPipesTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // Do not spawn oil pipes
            codeMatcher.MatchForward(false, 
                    new CodeMatch(OpCodes.Ldstr, "oilpipe"))
                .ThrowIfInvalid($"{nameof(RemoveOilPipesPatch)}.{nameof(DoNotSpawnOilPipesTranspiler)} could not find a match (oilpipe ldstr)!")
                .MatchBack(true, 
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(WorldGeneration), nameof(WorldGeneration.chunkWidth))))
                .ThrowIfInvalid($"{nameof(RemoveOilPipesPatch)}.{nameof(DoNotSpawnOilPipesTranspiler)} could not find a match (chunkWidth ldfld)!")
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Call, 
                        AccessTools.Method(typeof(RemoveOilPipesPatch), nameof(GetMultiplier))), 
                    new CodeInstruction(OpCodes.Mul));
            
            return codeMatcher.InstructionEnumeration();
        }
    }
}