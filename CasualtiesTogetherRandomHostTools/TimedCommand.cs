using System;
using System.Collections;
using System.Globalization;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class TimedCommand
{
    public static void Register()
    {
        var command = new Command("TimedCommand", "Execute a command after a set delay.", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckArgumentCount(args, 3);

            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
                throw new Exception("Delay must be a positive integer!");

            var title = args[2].Replace('_', ' ');
            var command = string.Join(" ", args, 3, args.Length - 3).Trim();

            Plugin.Instance.StartCoroutine(CountdownCoro(seconds, title, command));

        }, null,
        [
            ("delay", "execution delay in seconds"),
            ("title", "chat announcement title. Use '_' to disable the in-chat announcements"),
            ("command", "command to execute; Do NOT use underscores to join the arguments")
        ]);
        Con.RegisterCommand(command);
    }

    private static IEnumerator CountdownCoro(int seconds, string title, string command)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        var lastRemaining = seconds;

        if (string.IsNullOrWhiteSpace(title))
            title = null;

        if (title != null)
            Chat.Server_ChatAnnouncement($"{title} in {seconds} seconds!");
        Con.con.LogToConsole($"{title ?? command} in {seconds} seconds!");

        while (true)
        {
            yield return null;
            if (!Multiplayer.IsNetworkActiveAndIsServer())
                yield break;

            var remaining = (int)Mathf.Ceil(deadline - Time.realtimeSinceStartup);
            if (remaining <= 0)
                break;

            if (remaining != lastRemaining && (remaining <= 10 || (remaining <= 60 ? remaining % 15 == 0 : remaining % 60 == 0)))
            {
                if (title != null)
                    Chat.Server_ChatAnnouncement($"{title} in {remaining} seconds!");
                Con.con.LogToConsole($"{title ?? command} in {remaining} seconds!");
            }
            lastRemaining = remaining;
        }

        if (title != null)
            Chat.Server_ChatAnnouncement($"{title}!!!");
        Con.con.LogToConsole($"{title ?? command}!!!");
        Con.con.ExecuteCommand(command);
    }
}
