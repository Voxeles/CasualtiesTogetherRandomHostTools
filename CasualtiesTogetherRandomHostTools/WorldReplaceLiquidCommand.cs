using System;
using System.Collections.Generic;
using System.Linq;
using Together;

namespace CasualtiesTogetherRandomHostTools;

public static class WorldReplaceLiquidCommand
{
    private static readonly Dictionary<string, byte> FluidNameToByte;

    static WorldReplaceLiquidCommand()
    {
        var fluidByteToName = FluidManager.WorldFluidToLiquidID
            .Where(x => x.Key != 0 && x.Key != 6)
            .ToDictionary(x => (byte)x.Key, x => x.Value);
        fluidByteToName.Add(0, "air");
        fluidByteToName.Add(6, "magma");
        FluidNameToByte = fluidByteToName.ToDictionary(x => x.Value, x => x.Key);
    }

    public static void Register()
    {
        Con.RegisterCommand(new Command("WorldReplaceLiquid", "Replaces a given liquid tile with another", args =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.con.CheckArgumentCount(args, 2);

            if (args[1] == "@a")
            {
                if (!FluidNameToByte.TryGetValue(args[2], out byte target))
                    throw new Exception($"Invalid liquid \"{args[2]}\"!");

                WorldCorrupt.ReplaceAllLiquids(target);
            }
            else
            {
                if (!FluidNameToByte.TryGetValue(args[1], out byte source))
                    throw new Exception($"Invalid liquid \"{args[1]}\"!");

                if (!FluidNameToByte.TryGetValue(args[2], out byte target))
                    throw new Exception($"Invalid liquid \"{args[2]}\"!");

                WorldCorrupt.ReplaceLiquid(source, target);
            }

        }, new Dictionary<int, List<string>> {
            {0, ["@a", .. FluidNameToByte.Keys]},
            {1, [.. FluidNameToByte.Keys]}
        }, [
            ("source liquid tile", ""),
            ("new liquid tile", "")
        ]));
    }
}
