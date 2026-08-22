using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using LiteNetLib;

namespace CasualtiesTogetherRandomHostTools;

internal static class ChatTranslate
{
	private static object _lock = new();
	
	public static void Hijack(NetPlayer plr, string message)
	{
		var autoTranslate = AutoTranslate.Instance;
		if (KaizoAutoTranslate.ShouldTranslateNearSalad(autoTranslate, plr, out var saladDist))
			Task.Run(() => KaizoAutoTranslate.DoTranslateNearSaladAsync(autoTranslate, plr, message, saladDist));
		else if (autoTranslate.ShouldTranslate(plr))
			Task.Run(() => DoTranslateAsync(plr, message));
		else
			SendPlayerMessageRestored(plr, message);
	}

	private static async Task DoTranslateAsync(NetPlayer plr, string message)
	{
		SendPlayerMessageRestored(plr, await AutoTranslate.Instance.TranslateAsync(message) ?? message);
	}

	public static void SendPlayerMessageRestored(NetPlayer plr, string result)
	{
		if (string.IsNullOrWhiteSpace(result))
			return;
		lock (_lock)
		{
			bool devChatspy = Con._DEV_CHATSPY;
			int num = devChatspy ? 1 : (KrokoshaScavMultiplayer.is_dedicated_server ? 1 : 0);
			string tag = "";
			if (plr.IsDead())
				tag = Lang.MarkMsgAsLocaleKey("plr_chattag_dead");
			if (num != 0)
				Chat.LogMessage(Chat.TagName(plr.playername, tag), result);
			if (plr.IsAlive() && KrokoshaScavMultiplayer.rules.SpeechImpairedChat)
				result = plr.body.talker.DistortString(in result);
			if (num != 0)
			{
				Body body = plr.body;
				if (body != null)
				{
					Talker talker = body.talker;
					if (talker != null)
						talker.ForceNoSpeechImpairment(result, true);
				}
			}
			foreach (var target in NetPlayer.ClientIdToPlayerDict.Values)
			{
				if (devChatspy && target.is_local)
					continue;
				var message = result;
				var chattag = tag;
				if (Util.IsWorldGenerated() && target != plr)
				{
					if (plr.CanCommunicateWith_TextChat(target))
					{
						if (target.IsAlive())
							target.playerbody.HearinglossDistortMessage(plr.playerbody, ref message, ref chattag);
						if (string.IsNullOrWhiteSpace(message))
							continue;
					}
					else
						continue;
				}
				var writer = Net.CreateWriter(10098);
				writer.Put((byte) 0);
				writer.Put(plr.clientId);
				writer.Put(chattag);
				writer.Put(message);
				Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, in writer, target.clientId);
			}
		}
	}
}

[HarmonyPatch(typeof(Chat), nameof(Chat.Server_PlayerChatMessageSend))]
internal static class AutoTranslateChatPatch
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codeMatcher = new CodeMatcher(instructions);
		codeMatcher.MatchForward(false,
				new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(Con), nameof(Con._DEV_CHATSPY))),
				new CodeMatch(OpCodes.Stloc_3))
			.ThrowIfInvalid($"{nameof(AutoTranslateChatPatch)}.{nameof(Transpiler)} could not find match!")
			.Insert(
				new CodeInstruction(OpCodes.Ldloc_2),
				new CodeInstruction(OpCodes.Ldloc_1),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChatTranslate), nameof(ChatTranslate.Hijack))),
				new CodeInstruction(OpCodes.Ret));
		return codeMatcher.InstructionEnumeration();
	}
}