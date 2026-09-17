using System.Collections.Generic;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch]
public static class RunSettingsPatches
{
    public const string ClearSpawnLocationKey = "randomhosttools_clearspawnlocation";
    public const string StartingDepthKey = "randomhosttools_startingdepth";
    public const string GenerateBlockLineKey = "randomhosttools_generateblockline";
    //public const string KaizoModeKey = "randomhosttools_kaizomode";

    public const int StartingDepthMaxValue = 1000;

    public record struct RunSettingRecord(string DefaultText, RunSetting Setting, object DefaultValue)
    {
        public string DefaultText = DefaultText;
        public RunSetting Setting = Setting;
        public object DefaultValue = DefaultValue;
    }

    public static readonly Dictionary<string, RunSettingRecord> SettingsDict = new()
    {
        {
            ClearSpawnLocationKey,
            new RunSettingRecord(
                "Clear spawn location",
                new RunSettingBool(ClearSpawnLocationKey),
                false)
        },
        {
            StartingDepthKey,
            new RunSettingRecord(
                "Starting depth",
                new RunSettingFloat(StartingDepthKey)
                {
                    limits = new RangeF(0, StartingDepthMaxValue), wholeNum = true
                },
                0f)
        },
        {
            GenerateBlockLineKey,
            new RunSettingRecord(
                "Generate block line above starting depth",
                new RunSettingBool(GenerateBlockLineKey),
                false)
        }
        //{KaizoModeKey, new RunSettingRecord("Kaizo mode", new RunSettingBool(KaizoModeKey), false)}
    };

    [HarmonyPatch(typeof(PreRunScript), nameof(PreRunScript.Awake))]
    [HarmonyPrefix]
    private static void EnsureRegistered(PreRunScript __instance)
    {
        int startingIndex = FindSettingIndex("debugworld");
        startingIndex = startingIndex >= 0 ? startingIndex + 1 : RunSettings.settingTypes.Count;

        foreach (var key in SettingsDict.Keys)
        {
            if (FindSettingIndex(key) < 0)
            {
                RunSettings.settingTypes.Insert(startingIndex++, SettingsDict[key].Setting);
            }
        }

        foreach (var preset in RunSettings.presets)
        {
            if (preset == null)
                continue;

            foreach (var key in SettingsDict.Keys)
            {
                if (!preset.presetValues.ContainsKey(key))
                    preset.presetValues.Add(key, SettingsDict[key].DefaultValue);
            }
        }
    }

    [HarmonyPatch(typeof(Locale), nameof(Locale.GetOther))]
    [HarmonyPostfix]
    private static void AddLocaleDefault(string str, ref string __result)
    {
        const string prefix = "runset";
        if (str == null || str.Length <= prefix.Length)
            return;
        var rawKey = str.Remove(0, prefix.Length);
        if (SettingsDict.ContainsKey(rawKey) && str == __result)
        {
            __result = SettingsDict[rawKey].DefaultText;
        }
    }

    private static int FindSettingIndex(string key)
    {
        for (int i = 0; i < RunSettings.settingTypes.Count; ++i)
        {
            if (RunSettings.settingTypes[i].name == key)
                return i;
        }

        return -1;
    }
}
