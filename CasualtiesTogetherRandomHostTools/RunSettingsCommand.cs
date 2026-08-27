using System;
using System.Collections.Generic;
using System.Linq;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class RunSettingsCommand
{
    private static List<string> _cachedRunSettings = [
        "unchipped",
        "baselootdensity",
        "lootmultiplier",
        "basetrapdensity",
        "trapincrease",
        "ambientlight",
        "timelimit",
        "startingsupplies",
        "xpgain",
        "metabolismrate",
        "healingrate",
        "fracturepain",
        "bleedrate",
        "infectionspeed",
        "infectionchance",
        "fibrillationrate",
        "moodnormalizationrate",
        "bonuslimbarmor",
        "staminaregen",
        "attackdamage",
        "minigamehandshake",
        "sleepcyclespeed",
        "encumbrancecap",
        "strokes",
        "braindamagefx",
        "forcesleep",
        "lowmoodevents",
        "liquidpushing",
        "disfigurement",
        "nosleeprestrictions",
        "infinitelaststand",
        "traderchance",
        "traderitemamount",
        "traderrepoffset",
        "itemdecayrate",
        "lockpickprecision",
        "layermodifierchance",
        "timebetweenearthquakes",
        "temperatureoffset",
        "oreamount",
        "debugworld",
    ];

    private static bool _showedWarning = false;
    
    public static void Register()
    {
        var comm = new Command("RunSettings", "Get or set a run setting", args =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();

            var preRunScript = GameObject.FindObjectOfType<PreRunScript>();
            var runSettings = preRunScript != null ? preRunScript.runSettings : WorldGeneration.runSettings;

            if (runSettings == null)
                throw new Exception("Run settings is null!?");

            if (_cachedRunSettings.Count != runSettings.Count)
                _cachedRunSettings = runSettings.Keys.ToList();

            if (args.Length < 2)
                throw new Exception("Not enough arguments!");

            var setting = args[1];
            if (!runSettings.TryGetValue(setting, out var value))
                throw new Exception($"No such setting: \"{setting}\"!");
            
            if (args.Length < 3)
            {
                Con.con.LogToConsole($"{setting}: {value}");
                return;
            }

            object newValue;
            switch (value)
            {
                case bool:
                {
                    var b = bool.Parse(args[2]);
                    runSettings.Remove(setting);
                    runSettings.Add(setting, b);
                    newValue = b;
                    break;
                }
                case int:
                {
                    var i = int.Parse(args[2]);
                    runSettings.Remove(setting);
                    runSettings.Add(setting, i);
                    newValue = i;
                    break;
                }
                case float:
                {
                    var f = float.Parse(args[2]);
                    runSettings.Remove(setting);
                    runSettings.Add(setting, f);
                    newValue = f;
                    break;
                }
                default:
                    throw new Exception($"Unrecognized type \"{value.GetType()}\" for setting \"{setting}\"");
            }

            if (preRunScript != null)
            {
                preRunScript.runSettings = runSettings;
                preRunScript.UpdateAllSettingDisplays();

                if (Net.is_server)
                {
                    var perfs = new WorldgenPatches.RunPrefs();
                    perfs.ReadPrefs();
                    var settings = WorldgenPatches.CompileRunSettings();
                    var writer = Net.CreateWriter(NetmsgId.RunSettingsSync);
                    writer.Put(perfs);
                    writer.Put(settings);
                    Net.Server_SendToClients(DeliveryMethod.ReliableUnordered, writer, ServerMain.AllClientIdsExceptHost);
                }
            }
            else
            {
                if (!_showedWarning)
                {
                    Con.con.LogToConsole($"<color=yellow>WARNING: Changing the run settings mid-run can cause desync issues until you reload the save!</color>");
                    Con.con.LogToConsole($"<color=yellow>The recommended use for this command is on the main menu before starting new runs, or at the start of a new layer.</color>");
                    Con.con.LogToConsole($"<color=yellow>Change the settings using 'RunSettings', then save the game using 'saveandquit' and load the save to apply the new settings.</color>");
                    Con.con.LogToConsole($"<color=yellow>Nothing has been changed. Retype this command once you understand how to use it.</color>");
                    _showedWarning = true;
                    return;
                }
                WorldGeneration.runSettings = runSettings;
                // How to sync?
            }
            
            Con.con.LogToConsole($"Set {setting}: {newValue}");

        }, new Dictionary<int, List<string>> {
            { 0, _cachedRunSettings }
        }, [
            ("setting", "run setting"),
            ("new value", "optional, leave empty to read the current value")
        ]);
        Con.RegisterCommand(comm);
    }
}