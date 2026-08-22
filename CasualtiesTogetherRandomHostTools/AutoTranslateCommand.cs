using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

public static class AutoTranslateCommand
{
    public static void Register()
    {
        var comm = new Command("AutoTranslate", "Change auto translation settings", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();

            switch (args[1])
            {
                case "enabled" when args.Length < 3:
                    Con.con.LogToConsole($"Auto translate enabled: {AutoTranslate.Instance.ConfigAutoTranslateEnabled.Value}");
                    break;
                case "enabled" when args[2].ToLowerInvariant().Equals("true"):
                    AutoTranslate.Instance.ConfigAutoTranslateEnabled.Value = true;
                    Con.con.LogToConsole("Auto translate enabled!");
                    break;
                case "enabled" when args[2].ToLowerInvariant().Equals("false"):
                    AutoTranslate.Instance.ConfigAutoTranslateEnabled.Value = false;
                    Con.con.LogToConsole("Auto translate disabled!");
                    break;
                case "enabled":
                    throw new Exception("Bad value - expected 'true' or 'false'");
                
                case "deeplUrl" when args.Length < 3:
                    Con.con.LogToConsole($"Auto translate DeepL Url: {AutoTranslate.Instance.ConfigAutoTranslateUrl.Value}");
                    break;
                case "deeplUrl":
                    AutoTranslate.Instance.ConfigAutoTranslateUrl.Value = args[2];
                    Con.con.LogToConsole($"Auto translate set DeepL Url: {args[2]}!");
                    break;
                
                case "deeplKey" when args.Length < 3:
                    Con.con.LogToConsole($"Auto translate DeepL Key: {AutoTranslate.Instance.ConfigAutoTranslateKey.Value}");
                    break;
                case "deeplKey" when args[2].Length < 2:
                    AutoTranslate.Instance.ConfigAutoTranslateKey.Value = "";
                    Con.con.LogToConsole($"The key \"{args[2]}\" is not valid. Cleared key.");
                    break;
                case "deeplKey":
                    AutoTranslate.Instance.ConfigAutoTranslateKey.Value = args[2];
                    Con.con.LogToConsole($"Auto translate set DeepL key: {args[2]}!");
                    break;
                
                case "targetLang" when args.Length < 3:
                    Con.con.LogToConsole($"Auto translate target language: {AutoTranslate.Instance.ConfigAutoTranslateTargetLang.Value}");
                    break;
                case "targetLang":
                    AutoTranslate.Instance.ConfigAutoTranslateTargetLang.Value = args[2];
                    Con.con.LogToConsole($"Auto translate set target language: {args[2]}!");
                    break;
                
                case "forceDisableWhenKaizo" when args.Length < 3:
                    Con.con.LogToConsole($"Kaizo mode's auto translate: {AutoTranslate.Instance.ForceDisableWhenKaizo}");
                    break;
                case "forceDisableWhenKaizo" when args[2].ToLowerInvariant().Equals("true"):
                    AutoTranslate.Instance.ForceDisableWhenKaizo = true;
                    Con.con.LogToConsole("Kaizo mode's auto translate disabled!");
                    break;
                case "forceDisableWhenKaizo" when args[2].ToLowerInvariant().Equals("false"):
                    AutoTranslate.Instance.ForceDisableWhenKaizo = false;
                    Con.con.LogToConsole("Kaizo mode's auto translate enabled!");
                    break;
                case "forceDisableWhenKaizo":
                    throw new Exception("Bad value - expected 'true' or 'false'");
                
                default:
                    throw new Exception("Bad value - no such setting");
            }
            
        }, new Dictionary<int, List<string>> {
            {0, ["enabled", "deeplUrl", "deeplKey", "targetLang", "forceDisableWhenKaizo"]}
        }, [
            ("setting", "auto translation setting"), 
            ("value", "new value, leave blank to get current value")
        ]);
        Con.RegisterCommand(comm);

        comm = new Command("AutoTranslateExclude", "Which players to exclude from auto-translation", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckArgumentCount(args, 1);

            var (success, errMsg) = ServerMain._PerformActionOnPlayersByName(args[1], plr =>
            {
                var added = AutoTranslate.Instance.DoNotTranslateList.Add(plr);
                Con.con.LogToConsole(added
                    ? $"Player {plr.playername} excluded from auto-translation"
                    : $"Player {plr.playername} already excluded from auto-translation");
            });
            if (!success)
                Con.con.LogToConsole($"ERROR: AutoTranslateExclude: {args[1]}  - {errMsg}");

        }, null, ("player", ""));
        Con.RegisterCommand(comm);
        
        comm = new Command("AutoTranslateInclude", "Which players to include in auto-translation", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckArgumentCount(args, 1);

            var (success, errMsg) = ServerMain._PerformActionOnPlayersByName(args[1], plr =>
            {
                var removed = AutoTranslate.Instance.DoNotTranslateList.Remove(plr);
                Con.con.LogToConsole(removed
                    ? $"Player {plr.playername} included in auto-translation"
                    : $"Player {plr.playername} already included in auto-translation");
            });
            if (!success)
                Con.con.LogToConsole($"ERROR: AutoTranslateInclude: {args[1]}  - {errMsg}");

        }, null, ("player", ""));
        Con.RegisterCommand(comm);
    }
}