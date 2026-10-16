using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class SavePlayerStateCommand
{
    private static readonly List<string> _savedFiles = [];

    private static string SaveDirPath => Path.Combine(Application.persistentDataPath, "SavedPlayerStates");

    public static void UpdateFiles()
    {
        try
        {
            _savedFiles.Clear();
            var dirPath = SaveDirPath;
            var dir = Directory.CreateDirectory(dirPath);
            foreach (var fileInfo in dir.EnumerateFiles("*.json"))
                _savedFiles.Add(fileInfo.Name);
        }
        catch (Exception ex)
        {
            Plugin.PrintWarning("Failed to read files: " + ex);
        }
    }

    public static void Register()
    {
        Con.RegisterCommand(new Command("SavePlayerState", "Save a player's state (inventory, health, etc.)", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 1);

            var dirPath = SaveDirPath;
            Directory.CreateDirectory(dirPath);
            var timeStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            Con.PerformActionOnBodiesByName(args[1], body =>
            {
                var playername = SanitizePlayerName(body.playerName);
                var fileName = $"{playername}-{timeStr}.json";
                var file = Path.Combine(dirPath, fileName);
                var data = SavedPlayerState.Create(body);
                var str = data.Serialize();

                int i = 1;
                while (File.Exists(file))
                {
                    fileName = $"{playername}-{timeStr}_({i++}).json";
                    file = Path.Combine(dirPath, fileName);
                }

                File.WriteAllBytes(file, Encoding.UTF8.GetBytes(str));
                Con.con.LogToConsole($"Wrote {body.playerName} state to file: {file}");

            }, allow_macros: true, only_players: true, Con.GetCommandCallerPlayer(), onlyOne: false);

            UpdateFiles();

        }, null, [
            ("player", "player or players to whose state to save")
        ]));

        Con.RegisterCommand(new Command("LoadPlayerState", "Load a player's state", args =>
        {
            UpdateFiles();

            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 3);

            if (!Enum.TryParse(args[1], ignoreCase: true, out SavedPlayerState.RestoreSelection scope))
                throw new Exception($"Failed to parse scope \"{args[1]}\"");

            var filePath = Path.Combine(SaveDirPath, args[3]);
            if (!File.Exists(filePath))
                throw new Exception($"File {filePath} does not exist");

            var data = SavedPlayerState.Deserialize(File.ReadAllText(filePath));
            if (data == null)
                throw new Exception($"Failed to read file: {filePath}");

            Con.PerformActionOnBodiesByName(args[2], body =>
            {
                data.Apply(body, scope);
                ConsoleScript.instance.LogToConsole($"Loaded {body.playerName}'s state!");

            }, allow_macros: true, only_players: true, Con.GetCommandCallerPlayer(), onlyOne: false);

        }, new Dictionary<int, List<string>> {
            {0, Enum.GetNames(typeof(SavedPlayerState.RestoreSelection)).Select(s => s.ToLowerInvariant()).ToList()},
            {2, _savedFiles}
        }, [
            ("scope", "what to restore. concatenate with ',' (no spaces)"),
            ("player", "player whose state to restore"),
            ("file", "file to load")
        ]));
        UpdateFiles();

        Con.RegisterCommand(new Command("SavePlayerStateAuto", "Sets the auto player state save settings", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();

            ConsoleScript.instance.CheckArgumentCount(args, 1);

            var hasValue = args.Length > 2 && !string.IsNullOrEmpty(args[2]);
            var valueStr = hasValue ? args[2] : null;

            switch (args[1])
            {
                case "Enabled":
                {
                    if (!hasValue)
                    {
                        Con.con.LogToConsole($"AutoSavePlayerState.Enabled = {SavePlayerStateAutoWatcher.ConfigEnabled.Value}");
                        return;
                    }

                    if (!bool.TryParse(valueStr, out var boolValue))
                        throw new Exception($"Invalid value for 'Enabled': '{valueStr}'. Expected true or false.");

                    SavePlayerStateAutoWatcher.ConfigEnabled.Value = boolValue;
                    Con.con.LogToConsole($"AutoSavePlayerState.Enabled set to {boolValue}");
                    break;
                }

                case "PeriodInMinutes":
                {
                    if (!hasValue)
                    {
                        Con.con.LogToConsole($"AutoSavePlayerState.PeriodInMinutes = {SavePlayerStateAutoWatcher.ConfigPeriod.Value}");
                        return;
                    }

                    if (!int.TryParse(valueStr, out var intValue) || intValue <= 0)
                        throw new Exception($"Invalid value for 'PeriodInMinutes': '{valueStr}'. Expected a positive integer.");

                    SavePlayerStateAutoWatcher.ConfigPeriod.Value = intValue;
                    Con.con.LogToConsole($"AutoSavePlayerState.PeriodInMinutes set to {intValue} minutes");
                    break;
                }

                case "MaxStatesPerPlayer":
                {
                    if (!hasValue)
                    {
                        Con.con.LogToConsole($"AutoSavePlayerState.MaxStatesPerPlayer = {SavePlayerStateAutoWatcher.ConfigMaxStatesPerPlayer.Value}");
                        return;
                    }

                    if (!int.TryParse(valueStr, out var intValue) || intValue < 0)
                        throw new Exception($"Invalid value for 'MaxStatesPerPlayer': '{valueStr}'. Expected a non-negative integer.");

                    SavePlayerStateAutoWatcher.ConfigMaxStatesPerPlayer.Value = intValue;
                    Con.con.LogToConsole($"AutoSavePlayerState.MaxStatesPerPlayer set to {intValue}");
                    break;
                }

                default:
                    Con.con.LogToConsole($"Unknown setting '{args[1]}'. Valid settings: Enabled, PeriodInMinutes, MaxStatesPerPlayer");
                    break;
            }

        }, new Dictionary<int, List<string>> {
            {0, ["Enabled", "PeriodInMinutes", "MaxStatesPerPlayer"]}
        }, [
            ("setting", "setting to change"),
            ("value", "new value, leave blank to read current value")
        ]));
    }

    private static readonly HashSet<char> InvalidChars = [' ', '<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string SanitizePlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "empty_string";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(c <= '\u001F' || InvalidChars.Contains(c) ? '_' : c);

        return sb.ToString();
    }
}
