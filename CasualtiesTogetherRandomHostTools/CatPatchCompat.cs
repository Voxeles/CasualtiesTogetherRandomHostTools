using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class CatPatchCompat
{
    public const String CatPatchGuid = "meow.catpatch";
    private const string Namespace = "CatPatchspace.";

    internal static readonly bool Loaded;

    internal static readonly MethodInfo CanStartHug;
    internal static readonly Interaction Hug;
    internal static readonly Interaction Kiss;

    static CatPatchCompat()
    {
        if (!Chainloader.PluginInfos.ContainsKey(CatPatchGuid))
            return;

        CanStartHug = AccessTools.Method(AccessTools.TypeByName(Namespace + "HugValidation"), "CanStartHug");
        Hug = new Interaction("HugController", "TrySendHugRequest", "HugNetworkController", "HugVisualController");
        Kiss = new Interaction("KissController", "TrySendKissRequest", "KissNetworkController", "KissVisualController");

        Loaded = CanStartHug != null && Hug.IsValid && Kiss.IsValid;
        if (!Loaded)
            Plugin.Logger.LogWarning("CatPatch is loaded, but its hug/kiss methods could not be found. Clones won't be huggable.");
    }

    internal sealed class Interaction
    {
        internal readonly MethodInfo TrySendRequest;
        internal readonly MethodInfo SendPromptToClient;
        internal readonly MethodInfo VisualStart;

        private readonly FieldInfo _nextAllowedRequestAt;
        private readonly FieldInfo _networkController;
        private readonly MethodInfo _sendRequest;
        private readonly MethodInfo _handleServerResponse;

        internal Interaction(string controller, string trySendRequest, string networkController, string visualController)
        {
            var controllerType = AccessTools.TypeByName(Namespace + controller);
            var networkControllerType = AccessTools.TypeByName(Namespace + networkController);
            var visualControllerType = AccessTools.TypeByName(Namespace + visualController);
            if (controllerType == null || networkControllerType == null || visualControllerType == null)
                return;

            TrySendRequest = AccessTools.Method(controllerType, trySendRequest);
            SendPromptToClient = AccessTools.Method(networkControllerType, "SendPromptToClient");
            VisualStart = AccessTools.Method(visualControllerType, "Start");
            _nextAllowedRequestAt = AccessTools.Field(controllerType, "_nextAllowedRequestAt");
            _networkController = AccessTools.Field(controllerType, "_networkController");
            _sendRequest = AccessTools.Method(networkControllerType, "SendRequest");
            _handleServerResponse = AccessTools.Method(networkControllerType, "HandleServerResponse");
        }

        internal bool IsValid
            => TrySendRequest != null
               && SendPromptToClient != null
               && VisualStart != null
               && _nextAllowedRequestAt != null
               && _networkController != null
               && _sendRequest != null
               && _handleServerResponse != null;

        internal void SendRequest(object controller, knetid target)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < (double)_nextAllowedRequestAt.GetValue(controller))
                return;

            _nextAllowedRequestAt.SetValue(controller, now + 1.0);
            _sendRequest.Invoke(_networkController.GetValue(controller), [target]);
        }

        internal void Accept(object networkController, knetid target, knetid requester)
        {
            _handleServerResponse.Invoke(networkController, [target, requester, true]);
        }
    }
}
