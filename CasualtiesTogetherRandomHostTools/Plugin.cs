using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherRandomHostTools;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("KrokoshaCasualtiesMP")]
public class Plugin : BaseUnityPlugin
{
	public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
	public const string ModName = MyPluginInfo.PLUGIN_NAME;
	public const string ModVersion = MyPluginInfo.PLUGIN_VERSION;

	internal new static ManualLogSource Logger;
	private readonly Harmony _harmony = new(ModGuid);
	public static Plugin Instance { get; private set; } = null!;

	public static bool IsKaizoEnabled = false;

	public void Awake()
	{
		Logger = base.Logger;
		Instance = this;

		SavePlayerStateAutoWatcher.BindConfigs(this);

		_ = new AutoTranslate(this);

		gameObject.AddComponent<SavePlayerStateAutoWatcher>();

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
