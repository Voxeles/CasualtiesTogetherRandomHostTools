using System;
using System.Collections.Generic;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine.SceneManagement;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(Con), nameof(Con._RegisterMultiplayerConsoleCommands))]
internal static class ConPatches
{
    private static void Postfix()
    {
        ExplosionOverrideCommand.Register();
        AutoTranslateCommand.Register();
        PingSaladsCommand.Register();
        RunSettingsCommand.Register();
        
        var comm = new Command("KaizoEnabled", "Is kaizo game mode enabled", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            
            switch (args[1])
            {
                case "true":
                    Plugin.IsKaizoEnabled = true;
                    // HACK: Workaround for a bug where the solar flare modifier persist after layer change
                    // Force UnchippedIsIndividual to true
                    KrokoshaScavMultiplayer.rules.UnchippedIsIndividual = true;
                    KrokoshaScavMultiplayer.ApplyGameRules();
                    Con.con.LogToConsole("Enabled kaizo mode!");
                    break;
                case "false":
                    Plugin.IsKaizoEnabled = false;
                    Con.con.LogToConsole("Disabled kaizo mode!");
                    break;
                default:
                    throw new Exception("Bad value - expected true or false.");
            }
        }, new Dictionary<int, List<string>> {
            {0, ["true", "false"]}
        }, [
            ("enabled", "")
        ]);
        Con.RegisterCommand(comm);

        comm = new Command("FinalDestination", "\"No crafting, voyager only, final destination\"", _ =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.ConFailIfNotInMainMenu();
            
            WorldgenPatches._CheckIfCanLoadAWorld();
            WorldgenPatches.SetTutorialPlayerPrefs(false);
            WorldgenPatches.earthquake_enabled = false;
            WorldgenPatches.SetRadlinePlayerPrefs(false);
            WorldgenPatches.runsettings["ambientlight"] = 2;
            if (KrokoshaScavMultiplayer.IsNetworkActiveAndIsServer())
                ServerMain.Server_Announce_GAME_START();

            FinalDestinationPatch.ShouldDoTheOverride = true;
            SceneManager.LoadScene("SampleScene");
            
        }, null, null);
        Con.RegisterCommand(comm);
    }
}
