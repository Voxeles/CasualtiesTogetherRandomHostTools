using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using KrokoshaCasualtiesMP;
using Newtonsoft.Json;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public class SavePlayerStateAutoWatcher : MonoBehaviour
{
    public static ConfigEntry<bool> ConfigEnabled;
    public static ConfigEntry<int> ConfigPeriod;
    public static ConfigEntry<int> ConfigMaxStatesPerPlayer;

    private DateTime lastSaveTime = DateTime.MinValue;

    private static string SaveDirPathAuto => Path.Combine(Application.persistentDataPath, "SavedPlayerStates");

    public static void BindConfigs(Plugin plugin)
    {
        ConfigEnabled = plugin.Config.Bind(
            "AutoSavePlayerState",
            "Enabled",
            true,
            "Enabled the player state auto-save");
        ConfigPeriod = plugin.Config.Bind(
            "AutoSavePlayerState",
            "PeriodInMinutes",
            5,
            "The time, in minutes, between saves");
        ConfigMaxStatesPerPlayer = plugin.Config.Bind(
            "AutoSavePlayerState",
            "MaxStatesPerPlayer",
            5,
            "The maximum amount of auto-saved player states per player before the oldest get deleted");
    }

    private void LateUpdate()
    {
        if (!ConfigEnabled.Value)
            return;

        if (!KrokoshaScavMultiplayer.network_system_is_running || KrokoshaScavMultiplayer.is_client)
            return;

        if (WorldGeneration.world == null || WorldGeneration.world.generatingWorld)
            return;

        if (DateTime.UtcNow - lastSaveTime < TimeSpan.FromMinutes(ConfigPeriod.Value))
            return;
        lastSaveTime = DateTime.UtcNow;

        var dirPath = SaveDirPathAuto;
        Directory.CreateDirectory(dirPath);
        var timeStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        int saveCount = 0;

        foreach (var netBody in NetBody.all_instances)
        {
            try
            {
                var playername = SavePlayerStateCommand.SanitizePlayerName(netBody.playername);
                var fileName = $"{playername}-auto-{timeStr}.json";
                var file = Path.Combine(dirPath, fileName);
                var data = SavedPlayerState.Create(netBody);
                var str = data.Serialize();

                int i = 1;
                while (File.Exists(file))
                {
                    fileName = $"{playername}-auto-{timeStr}_({i++}).json";
                    file = Path.Combine(dirPath, fileName);
                }

                File.WriteAllBytes(file, Encoding.UTF8.GetBytes(str));

                saveCount++;

                PruneOldAutoSaves(dirPath, playername);
            }
            catch (Exception e)
            {
                Plugin.PrintError($"Failed to auto-save {netBody.playername}: {e}");
            }
        }

        Con.con.LogToConsole($"Player state backup complete. Saved {saveCount} player states.");

        SavePlayerStateCommand.UpdateFiles();
    }

    private static void PruneOldAutoSaves(string dirPath, string playerName)
    {
        var maxStates = ConfigMaxStatesPerPlayer.Value;
        if (maxStates <= 0)
            return;

        playerName = SavePlayerStateCommand.SanitizePlayerName(playerName);

        var pattern = $"{playerName}-auto-*.json";

        FileInfo[] files;
        try
        {
            files = new DirectoryInfo(dirPath)
                .GetFiles(pattern)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToArray();
        }
        catch (Exception e)
        {
            Plugin.PrintError($"Failed to list auto-saves for {playerName}: {e}");
            return;
        }

        if (files.Length <= maxStates)
            return;

        foreach (var stale in files.Skip(maxStates))
        {
            try
            {
                stale.Delete();
            }
            catch (Exception e)
            {
                Plugin.PrintError($"Failed to delete old auto-save {stale.Name}: {e}");
            }
        }
    }
}
