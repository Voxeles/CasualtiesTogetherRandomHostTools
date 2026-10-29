using System;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class CloneCommand
{
    public static void Register()
    {
        Con.RegisterCommand(new Command("SpawnClone", "Spawn a clone of yourself", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckForWorld();

            if (CloneManager.IsRemoving)
                throw new Exception("Clones are being removed - please wait!");

            var host = NetBody.GetLocalNetBodyNullable();
            if (!host)
                throw new Exception("Host doesn't have a body!");

            var pos = args.Length > 1
                ? Con.con.ParsePosition(args[1])
                : (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);

            var clone = CloneManager.Spawn(host, pos);
            Con.con.LogToConsole($"Spawned clone (player id {clone.PlayerId})!");
        }, null, [
            ("position", "spawn position, defaults to the mouse position")
        ]));

        Con.RegisterCommand(new Command("RemoveClones", "Remove all clones", _ =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckForWorld();

            if (CloneManager.IsRemoving)
                throw new Exception("Clones are already being removed - please wait!");

            int count = CloneManager.RemoveAll();
            Con.con.LogToConsole(count == 0 ? "No clones to remove." : $"Removing {count} clone(s)...");
        }, null));

        Con.RegisterCommand(new Command("UpdateClones", "Update the body state of all clones to match yours", _ =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckForWorld();

            if (CloneManager.IsRemoving)
                throw new Exception("Clones are being removed - please wait!");

            var host = NetBody.GetLocalNetBodyNullable();
            if (!host)
                throw new Exception("Host doesn't have a body!");

            CloneManager.ApplyCloneStateAll(host);

            Con.con.LogToConsole("Updated the state of all clones!");
        }, null));
    }
}
