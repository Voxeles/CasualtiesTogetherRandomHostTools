using System;
using System.Threading.Tasks;
using KrokoshaCasualtiesMP;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesTogetherRandomHostTools;

public static class KaizoAutoTranslate
{
    public static bool ShouldTranslateNearSalad(AutoTranslate translate, NetPlayer player, out float saladDist)
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

    public static async Task DoTranslateNearSaladAsync(AutoTranslate translate, NetPlayer plr, string message, float dist)
    {
        ChatTranslate.SendPlayerMessageRestored(plr, dist switch
        {
            < 22 => await translate.TranslateAsync(message, "ZH") ?? TextScramble(message, 1f),
            < 42 => await translate.TranslateAsync(message, "FI") ?? TextScramble(message, 0.5f),
            < 62 => await translate.TranslateAsync(message, "CS") ?? TextScramble(message, 0.3f),
            < 82 => await translate.TranslateAsync(message, "NL") ?? TextScramble(message, 0.2f),
            < 102 => await translate.TranslateAsync(message, "PT") ?? TextScramble(message, 0.1f),
            _ => null
        });
    }
    
    private static string TextScramble(string message, float chance)
    {
        // Yes this corrupts the low and high surrogates, do I look like I care?
        var output = message.ToCharArray();
        for (var i = 0; i < output.Length; i++)
        {
            if (Random.value > chance)
                continue;
            output[i] = message[Random.RandomRangeInt(0, message.Length)];
        }
        return string.Join("", output);
    }
}