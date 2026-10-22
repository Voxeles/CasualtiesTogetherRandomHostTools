using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace CasualtiesTogetherRandomHostTools;

public static class MsrCompat
{
    public const String MsrGuid = "noah.casualtiesunknown.multiplayerspritereplacer";

    internal static readonly bool Loaded;

    internal static readonly MethodInfo TryGetTargetClientId;
    internal static readonly MethodInfo RelaySpritesFromServer;
    internal static readonly MethodInfo SyncAllSpritesToClient;

    private static readonly MethodInfo QueueRefreshMethod;
    private static readonly MethodInfo LocalClientIdGetter;
    private static readonly FieldInfo CompressedPngData;
    private static readonly IDictionary PlayerSprites; // Dictionary<uint, Dictionary<string, CachedSprite>>
    private static readonly HashSet<uint> ModdedClients;
    private static readonly object SpriteLock;

    static MsrCompat()
    {
        if (!Chainloader.PluginInfos.TryGetValue(MsrGuid, out var info) || info.Instance == null)
            return;

        var assembly = info.Instance.GetType().Assembly;
        var spriteApplication = assembly.GetType("SpriteApplication");
        var spriteNetwork = assembly.GetType("SpriteNetwork");
        var spriteCache = assembly.GetType("SpriteCache");
        var cachedSprite = assembly.GetType("CachedSprite");
        if (spriteApplication != null && spriteNetwork != null && spriteCache != null && cachedSprite != null)
        {
            TryGetTargetClientId = AccessTools.Method(spriteApplication, "TryGetTargetClientId");
            QueueRefreshMethod = AccessTools.Method(spriteApplication, "QueueRefresh");
            RelaySpritesFromServer = AccessTools.Method(spriteNetwork, "RelaySpritesFromServer");
            SyncAllSpritesToClient = AccessTools.Method(spriteNetwork, "SyncAllSpritesToClient");
            LocalClientIdGetter = AccessTools.PropertyGetter(spriteNetwork, "LocalClientId");
            CompressedPngData = AccessTools.Field(cachedSprite, "CompressedPngData");
            PlayerSprites = AccessTools.Field(spriteCache, "PlayerSprites")?.GetValue(null) as IDictionary;
            ModdedClients = AccessTools.Field(spriteCache, "ModdedClients")?.GetValue(null) as HashSet<uint>;
            SpriteLock = AccessTools.Field(spriteCache, "SpriteLock")?.GetValue(null);
        }

        Loaded = TryGetTargetClientId != null
                 && QueueRefreshMethod != null
                 && RelaySpritesFromServer != null
                 && SyncAllSpritesToClient != null
                 && LocalClientIdGetter != null
                 && CompressedPngData != null
                 && PlayerSprites != null
                 && ModdedClients != null
                 && SpriteLock != null;
        if (!Loaded)
            Plugin.Logger.LogWarning("Multiplayer Sprite Replacer is loaded, but its methods could not be found. Clones won't use the host's skin.");
    }

    internal static uint LocalClientId
        => (uint)LocalClientIdGetter.Invoke(null, null);

    internal static void QueueRefresh()
    {
        QueueRefreshMethod.Invoke(null, null);
    }

    internal static void RelaySprites(uint ownerClientId, bool replaceSet, List<KeyValuePair<string, byte[]>> sprites, uint excludeClientId)
    {
        RelaySpritesFromServer.Invoke(null, [ownerClientId, replaceSet, sprites, excludeClientId]);
    }

    internal static List<KeyValuePair<string, byte[]>> GetSprites(uint clientId)
    {
        var sprites = new List<KeyValuePair<string, byte[]>>();
        lock (SpriteLock)
        {
            if (!ModdedClients.Contains(clientId) || PlayerSprites[clientId] is not IDictionary dict)
                return sprites;

            foreach (DictionaryEntry entry in dict)
                sprites.Add(new KeyValuePair<string, byte[]>((string)entry.Key, (byte[])CompressedPngData.GetValue(entry.Value)));
        }
        return sprites;
    }

    // Adds an player entry 'id' to the sprite cache, with the same sprites as 'sourceid'
    // MAKE SURE to remove the addition with RemoveAlias!!! MSR destroys the sprites once the player associated with 'sourceid' leaves!
    internal static bool TryAlias(uint id, uint sourceId)
    {
        lock (SpriteLock)
        {
            if (PlayerSprites.Contains(id) || !PlayerSprites.Contains(sourceId) || !ModdedClients.Contains(sourceId))
                return false;

            PlayerSprites[id] = PlayerSprites[sourceId];
            ModdedClients.Add(id);
            return true;
        }
    }

    internal static void RemoveAlias(uint id)
    {
        lock (SpriteLock)
        {
            PlayerSprites.Remove(id);
            ModdedClients.Remove(id);
        }
    }
}
