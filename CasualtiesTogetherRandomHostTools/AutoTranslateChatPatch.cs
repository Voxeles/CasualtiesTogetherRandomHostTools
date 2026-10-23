using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using LiteNetLib.Utils;
using Together;

namespace CasualtiesTogetherRandomHostTools;

internal static class ChatTranslate
{
	public static void Hijack(knetid clientId, NetDataReader reader)
	{
		if (!ScavPlayer.TryGetPlayerFromClientId(clientId, out var plr))
			return;

		var buf = new byte[reader.RawDataSize];
		Array.Copy(reader.RawData, buf, reader.RawDataSize);
		var originalReader = new NetDataReader(buf);
		originalReader.SetPosition(AutoTranslateChatPatch.OriginalReaderPos);

		string message;
		try
		{
			int expectedEnd = reader.Position;
			reader.SetPosition(AutoTranslateChatPatch.OriginalReaderPos);
			reader.Get(out bool _);
			reader.Get(out bool _);
			reader.Get(out knetid _);
			reader.Get(out message);

			if (reader.Position != expectedEnd)
				throw new Exception($"Chat message packet format changed: expected {expectedEnd}, extracted {reader.Position} - abort!");
		}
		catch (Exception ex)
		{
			Plugin.PrintError($"Hijack failed to extract message: {ex}");
			originalReader.SetPosition(AutoTranslateChatPatch.OriginalReaderPos);
			AutoTranslateChatPatch.OriginalMethod(clientId, originalReader);
			return;
		}

		var autoTranslate = AutoTranslate.Instance;
		if (KaizoAutoTranslate.ShouldTranslateNearSalad(autoTranslate, plr, out var saladDist))
			Task.Run(() => KaizoAutoTranslate.DoTranslateNearSaladAsync(autoTranslate, plr, message, saladDist, originalReader));
		else if (autoTranslate.ShouldTranslate(plr))
			Task.Run(() => DoTranslateAsync(plr, message, originalReader));
		else
			SendPlayerMessageRestored(plr, message, originalReader);
	}

	public static async Task DoTranslateAsync(ScavPlayer plr, string message, NetDataReader reader)
	{
		var translated = await AutoTranslate.Instance.TranslateAsync(message) ?? message;
		SendPlayerMessageRestored(plr, translated, reader);
	}

	public static void SendPlayerMessageRestored(ScavPlayer plr, string newMessage, NetDataReader originalReader)
	{
		originalReader.Get(out bool isTeam);
		originalReader.Get(out bool isWhisper);
		originalReader.Get(out knetid whisperTarget);

		var writer = new NetDataWriter();
		writer.Put(isTeam);
		writer.Put(isWhisper);
		writer.Put(whisperTarget);
		writer.Put(newMessage);

		Plugin.MainThreadContext.Post(_ => AutoTranslateChatPatch.OriginalMethod(plr.playerId, new NetDataReader(writer)), null);
	}
}

[HarmonyPatch(typeof(Chat), nameof(Chat.Server_PlayerChatMessageSend))]
internal static class AutoTranslateChatPatch
{
	public static int OriginalReaderPos;

	[HarmonyReversePatch]
	public static void OriginalMethod(knetid clientId, NetDataReader reader) => throw new NotImplementedException();

	private static void Prefix(NetDataReader reader)
	{
		OriginalReaderPos = reader.Position;
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codeMatcher = new CodeMatcher(instructions);
		codeMatcher
			.MatchForward(false,
				new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Chat), nameof(Chat.SHOULD_LOG_CHAT))))
			.ThrowIfInvalid($"{nameof(AutoTranslateChatPatch)}.{nameof(Transpiler)} could not find match!");
		var oldEntryPoint = codeMatcher.Instruction;

		codeMatcher
			.Insert(
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldarg_1),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChatTranslate), nameof(ChatTranslate.Hijack))),
				new CodeInstruction(OpCodes.Ret))
			.Instruction.MoveLabelsFrom(oldEntryPoint);

		return codeMatcher.InstructionEnumeration();
	}
}
