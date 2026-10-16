using System;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class CloneCommand
{
    public static void Register()
    {
        var command = new Command("SpawnClone", "Spawn a clone of yourself", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckForWorld();

            var host = ScavPlayer.GetLocalNetBodyNullable();
            if (!host)
                throw new Exception("You don't have a body!");

            var pos = args.Length > 1
                ? Con.con.ParsePosition(args[1])
                : (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);

            var clone = CloneManager.Spawn(host, pos);
            Con.con.LogToConsole($"Spawned clone (player id {clone.PlayerId})!");
        }, null, [
            ("position", "spawn position, defaults to the mouse position")
        ]);
        Con.RegisterCommand(command);

        command = new Command("RemoveClones", "Remove all clones", _ =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();

            int count = CloneManager.RemoveAll();
            Con.con.LogToConsole($"Removed {count} clone(s).");
        }, null);
        Con.RegisterCommand(command);
    }
}
