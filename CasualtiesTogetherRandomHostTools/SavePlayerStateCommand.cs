using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KrokoshaCasualtiesMP;
using Newtonsoft.Json;
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
            Plugin.Logger.LogWarning("Failed to read files: " + ex);
        }
    }

    public static void Register()
    {
        var comm = new Command("SavePlayerState", "Save a player's state (inventory, health, skills)", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 1);

            var dirPath = SaveDirPath;
            Directory.CreateDirectory(dirPath);
            var timeStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            ServerMain._PerformActionOnBodiesByName(args[1], body =>
            {
                var playername = SanitizePlayerName(body.playername);
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
                Con.con.LogToConsole($"Wrote {body.playername} state to file: {file}");
            });

            UpdateFiles();

        }, null, [
            ("player", "player or players to whose state to save")
        ]);
        Con.RegisterCommand(comm);

        comm = new Command("LoadPlayerState", "Load a player's state", args =>
        {
            UpdateFiles();

            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 2);

            var filePath = Path.Combine(SaveDirPath, args[2]);
            if (!File.Exists(filePath))
                throw new Exception($"File {filePath} does not exist");

            Plugin.Logger.LogInfo($"Loading: {filePath}");

            var data = SavedPlayerState.Deserialize(File.ReadAllText(filePath));
            if (data == null)
                throw new Exception($"Failed to read file: {filePath}");

            var netBody = ServerMain.RelaxedGetBodyForCommand(args[1], false, true);

            Plugin.Logger.LogInfo($"Applying: {data}");
            data.Apply(netBody);

            ConsoleScript.instance.LogToConsole($"Loaded {args[1]}'s state!");

        }, new Dictionary<int, List<string>> {
            {1, _savedFiles}
        }, [
            ("player", "player whose state to restore"),
            ("file", "file to load")
        ]);
        Con.RegisterCommand(comm);
        UpdateFiles();

        comm = new Command("SavePlayerStateAuto", "Sets the auto player state save settings", args =>
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
        ]);
        Con.RegisterCommand(comm);
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
