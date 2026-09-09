using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

public static class SyncTimerCommand
{
    public static void Register()
    {
        var comm = new Command("SyncTimerOverride", "Override the sync timers wait period.", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();

            if (args.Length < 3)
                throw new Exception("Not enough arguments - Expected a timer name and a decimal value!");

            var value = float.Parse(args[2]);
            
            switch (args[1])
            {
                case "tilemap":
                    SyncTimerPatches.TilemapSyncTime = value;
                    Con.con.LogToConsole(value >= 0f
                        ? $"Updated the tilemap timer period to {value} seconds!"
                        : $"Using the default value for the tilemap timer ({SyncTimerPatches.TilemapSyncTimeDefault})!");
                    break;
                case "fluid":
                    SyncTimerPatches.FluidTilemapSyncTime = value;
                    Con.con.LogToConsole(value >= 0f
                        ? $"Updated the fluid tilemap timer period to {value} seconds!"
                        : $"Using the default value for the fluid tilemap timer ({SyncTimerPatches.FluidTilemapSyncTimeDefault})!");
                    break;
                case "objectFast":
                    SyncTimerPatches.ObjectFastSyncTime = value;
                    Con.con.LogToConsole(value >= 0f
                        ? $"Updated the fast object timer period to {value} seconds!"
                        : $"Using the default value for the fast object timer ({SyncTimerPatches.ObjectFastSyncTimeDefault})!");
                    break;
                case "objectNormal":
                    SyncTimerPatches.ObjectNormalSyncTime = value;
                    Con.con.LogToConsole(value >= 0f
                        ? $"Updated the normal object timer period to {value} seconds!"
                        : $"Using the default value for the normal object timer ({SyncTimerPatches.ObjectNormalSyncTimeDefault})!");
                    break;
                case "objectRare":
                    SyncTimerPatches.ObjectRareSyncTime = value;
                    Con.con.LogToConsole(value >= 0f
                        ? $"Updated the rare object timer period to {value} seconds!"
                        : $"Using the default value for the rare object timer ({SyncTimerPatches.ObjectRareSyncTimeDefault})!");
                    break;
                default:
                    throw new Exception("Invalid timer name!");
            }

        }, new Dictionary<int, List<string>>
        {
            {0, ["tilemap", "fluid", "objectFast", "objectNormal", "objectRare"]}
        }, [
            ("timer", "which timer to get/set"),
            ("sync time", "a decimal number representing the new sync time value in seconds. Set it to -1 to use the default.")
        ]);
        Con.RegisterCommand(comm);
    }
}
