using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInDependency("KrokoshaCasualtiesMP")]
public class Plugin : BaseUnityPlugin
{
	public const string ModGUID = "cump.random.host.tools";
	public const string ModName = "CasualtiesTogetherRandomHostTools";
	public const string ModVersion = "0.0.2";

	internal new static ManualLogSource Logger;
	private readonly Harmony _harmony = new(ModGUID);
	public static Plugin Instance { get; private set; } = null!;

	public static bool IsKaizoEnabled = false;

	public void Awake()
	{
		Logger = base.Logger;
		Instance = this;

		_ = new AutoTranslate(this);

		_harmony.PatchAll();

		Logger.LogInfo($"Plugin {ModName} is loaded!");
	}

	public void OnDestroy()
	{
		_harmony?.UnpatchSelf();
		Instance = null;
	}

	internal static void PrintError(string message)
	{
		Con.con.LogToConsole($"<color=red>{ModName}: ERROR: {message}</color>");
		Logger.LogError($"ERROR: {message}");
	}
}

