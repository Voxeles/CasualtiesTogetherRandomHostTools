using System;
using System.Collections.Generic;
using System.Globalization;
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
        SyncTimerCommand.Register();
        SavePlayerStateCommand.Register();
        SaveWorldTilesCommand.Register();
        
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

        comm = new Command("FinalDestination", "\"No crafting, voyager only, final destination\"", args =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.ConFailIfNotInMainMenu();

            if (args.Length > 1)
                FinalDestinationPatch.ActuallyDoBattlefield = bool.Parse(args[1]);
            if (args.Length > 2)
                FinalDestinationPatch.Scaling = bool.Parse(args[2]) ? 2 : 1;

            WorldgenPatches._CheckIfCanLoadAWorld();
            WorldgenPatches.SetTutorialPlayerPrefs(false);
            WorldgenPatches.earthquake_enabled = false;
            WorldgenPatches.SetRadlinePlayerPrefs(false);
            WorldgenPatches.runsettings["ambientlight"] = 2;
            if (KrokoshaScavMultiplayer.IsNetworkActiveAndIsServer())
                ServerMain.Server_Announce_GAME_START();

            FinalDestinationPatch.ShouldDoTheOverride = true;
            SceneManager.LoadScene("SampleScene");
            
        }, null, ("bool", "Add platforms (battlefield)"), ("bool", "Double the width"));
        Con.RegisterCommand(comm);

        comm = new Command("SaladSplitCount", "Maximum amount of times that salads can split when kaizo mode is enabled", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckArgumentCount(args, 1);

            SaladSplitter.MaximumSplitCount = int.Parse(args[1], CultureInfo.InvariantCulture);

            Con.con.LogToConsole($"Set the maximum split count to {SaladSplitter.MaximumSplitCount}!");

        }, new Dictionary<int, List<string>> {
            {0, ["1"]}
        }, [
            ("integer", "maximum amount of splits")
        ]);
        Con.RegisterCommand(comm);
    }
}
