using System.Threading.Tasks;
using LiteNetLib.Utils;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class KaizoAutoTranslate
{
    public static bool ShouldTranslateNearSalad(AutoTranslate translate, ScavPlayer player, out float saladDist)
    {
        saladDist = 0f;
        if (!Plugin.IsKaizoEnabled || translate.ForceDisableWhenKaizo)
            return false;
        if (!PlayerCamera.main || WorldGeneration.world == null || WorldGeneration.world.generatingWorld)
            return false;
        if (!player.IsAlive())
            return false;
        
        var salads = GameObject.FindObjectsByType<ElderThornbackBehaviour>(FindObjectsSortMode.None);
        if (salads == null || salads.Length == 0)
            return false;

        var playerPos = player.pos;
        var closestSaladDist = float.PositiveInfinity;
        
        foreach (var salad in salads)
        {
            var dist = Vector2.Distance(salad.transform.position, playerPos);
            if (dist < closestSaladDist)
                closestSaladDist = dist;
        }

        saladDist = closestSaladDist;
        return saladDist < 102;
    }

    public static async Task DoTranslateNearSaladAsync(AutoTranslate translate, ScavPlayer plr, string message, float dist, NetDataReader reader)
    {
        var translated = dist switch
        {
            < 22 => await translate.TranslateAsync(message, AutoTranslate.Language.Chinese) ?? TextScramble(message, 1f),
            < 42 => await translate.TranslateAsync(message, AutoTranslate.Language.Finnish) ?? TextScramble(message, 0.5f),
            < 62 => await translate.TranslateAsync(message, AutoTranslate.Language.Czech) ?? TextScramble(message, 0.3f),
            < 82 => await translate.TranslateAsync(message, AutoTranslate.Language.Dutch) ?? TextScramble(message, 0.2f),
            < 102 => await translate.TranslateAsync(message, AutoTranslate.Language.Portuguese) ?? TextScramble(message, 0.1f),
            _ => null
        };
        ChatTranslate.SendPlayerMessageRestored(plr, translated, reader);
    }
    
    private static string TextScramble(string message, float chance)
    {
        var output = message.ToCharArray();

        var rnd = new System.Random(); // Don't use Unity.Random. It cannot be used on a job thread.

        for (var i = 0; i < output.Length; i++)
        {
            if (rnd.NextDouble() > chance)
                continue;
            // Yes this corrupts the low and high surrogates, but idgaf
            output[i] = message[rnd.Next(0, message.Length)];
        }
        return new string(output);
    }
}
