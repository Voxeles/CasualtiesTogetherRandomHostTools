using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

public static class ExplosionOverrideCommand
{
    // I know, I should have used reflection... I'm not a C# programmer by-day okay!?!
    // ExplosionParams members:
    // RangeF muscleDamage = new RangeF(0.0f, 60f);
    // RangeF skinDamage = new RangeF(0.0f, 75f);
    // float skinDamageChance = 0.2f;
    // float boneBreakChance = 0.06f;
    // float dislocationChance = 0.135f;
    // float disfigureChance = 0.34f;
    // float bleedChance = 0.15f;
    // RangeF bleedAmount = new RangeF(4f, 30f);
    // float structuralDamage = 500f;
    // Vector2 position;
    // float range = 12f;
    // float velocity = 60f;
    // float shrapnelChance = 0.4f;
    // string sound = "explosion";
    
    private static readonly List<Action<ExplosionParams, string>> ExplosionParamSetters =
    [
        (p, a) => p.range = float.Parse(a),
        (p, a) => p.velocity = float.Parse(a),
        (p, a) => p.muscleDamage.min = float.Parse(a),
        (p, a) => p.muscleDamage.max = float.Parse(a),
        (p, a) => p.skinDamage.min = float.Parse(a),
        (p, a) => p.skinDamage.max = float.Parse(a),
        (p, a) => p.skinDamageChance = float.Parse(a),
        (p, a) => p.structuralDamage = float.Parse(a),
        (p, a) => p.boneBreakChance = float.Parse(a),
        (p, a) => p.dislocationChance = float.Parse(a),
        (p, a) => p.disfigureChance = float.Parse(a),
        (p, a) => p.bleedChance = float.Parse(a),
        (p, a) => p.bleedAmount.min = float.Parse(a),
        (p, a) => p.bleedAmount.max = float.Parse(a),
        (p, a) => p.shrapnelChance = float.Parse(a),
        (p, a) => p.sound = a,
    ];

    private static readonly Dictionary<int, List<string>> ExplosionParamDefaults = new()
    {
        {0, ["12"]},
        {1, ["60"]},
        {2, ["0"]},
        {3, ["60"]},
        {4, ["0"]},
        {5, ["75"]},
        {6, ["0.2"]},
        {7, ["500"]},
        {8, ["0.06"]},
        {9, ["0.135"]},
        {10, ["0.34"]},
        {11, ["0.15"]},
        {12, ["4"]},
        {13, ["30"]},
        {14, ["0.4"]},
        {15, ["explosion"]},
    };

    private static readonly (string, string)[] ExplosionParamDescs =
    [
        ("range", "Default: 12"),
        ("velocity", "optional, Default: 60"),
        ("muscleDamageMin", "optional, Default: 0"),
        ("muscleDamageMax", "optional, Default: 60"),
        ("skinDamageMin", "optional, Default: 0"),
        ("skinDamageMax", "optional, Default: 75"),
        ("skinDamageChance", "optional, Default: 0.2"),
        ("structuralDamage", "optional, Default: 500"),
        ("boneBreakChance", "optional, Default: 0.06"),
        ("dislocationChance", "optional, Default: 0.135"),
        ("disfigureChance", "optional, Default: 0.34"),
        ("bleedChance", "optional, Default: 0.15"),
        ("bleedAmountMin", "optional, Default: 4"),
        ("bleedAmountMax", "optional, Default: 30"),
        ("shrapnelChance", "optional, Default: 0.4"),
        ("sound", "optional, Default: explosion")
    ];

    public static void Register()
    {
        var comm = new Command("ExplosionOverride", "Override explosion params, leave empty to disable, you _do not_ have to fill all settings", args =>
        {
            Con.ConFailIfNetworkNotRunning();
            Con.ConFailIfNetworkIsRunningAndIsClient();
            Con.ConFailIfCheatsDisabled();

            if (args.Length == 1)
            {
                ExplosionPatches.ParamsOverride = null;
                Con.con.LogToConsole("Disabled explosion params override");
                return;
            }

            ExplosionPatches.ParamsOverride = new ExplosionParams();
            var p = ExplosionPatches.ParamsOverride;

            for (int i = 0; i < ExplosionParamSetters.Count && i + 1 < args.Length; i++)
                ExplosionParamSetters[i](p, args[i + 1]);
            
            Con.con.LogToConsole("Set new explosion params!");

        }, ExplosionParamDefaults, ExplosionParamDescs);
        Con.RegisterCommand(comm);
    }
}