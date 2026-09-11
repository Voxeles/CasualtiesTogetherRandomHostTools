using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesTogetherRandomHostTools;

// Issues:
// - Mp mod has no way of syncing favourited items.
// Save the favourited status, but don't restore it.
// Otherwise, desync occurs where clients can't use their items for crafting, even though they have it unfavourited.
// - Mp mod has no way of syncing crafted recipes.
// Save and restore it. That way, INT xp for crafting is still awarded correctly, but the Client needs to remember what they crafted previously.
// - Body components seem useless with MP mod's health and painkiller packets
// Save the body components, but don't restore them. There might be other body components I missed.
// Painkillers and mindwipe are handled by MP mod's packets. I don't know of any others.

public sealed class SavedPlayerState
{
    public List<SavedItem> SavedItemList = [];
    public Dictionary<int, Dictionary<Type, string>> ComponentsDictionary = [];
    public List<Dictionary<Type, string>> BodyComponentsDictionaryUnused = [];
    public CharacterHealthPainkillerStateSyncPacket Painkillers;
    public CharacterHealthStateSyncPacket Health;
    public List<int> RecipesCrafted = [];

    public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        ContractResolver = new SavedPlayerStateContractResolver()
    };

    [Flags]
    public enum RestoreSelection
    {
        All = Inventory | Health | Crafting,
        Inventory = 1 << 0,
        Health = 1 << 1,
        Crafting = 1 << 2,
    }

    private static Dictionary<Type, string> SerializeComponents(Component item)
    {
        return item.gameObject.GetComponents<Component>()
            .Where(x => Attribute.GetCustomAttributes(x.GetType()).Any(attr => attr is Saveable))
            .ToDictionary(component => component.GetType(), component => JsonConvert.SerializeObject(component));
    }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented, JsonSettings);
    }

    public static SavedPlayerState Deserialize(string str)
    {
        return JsonConvert.DeserializeObject<SavedPlayerState>(str, JsonSettings);
    }

    public static SavedPlayerState Create(NetBody netBody)
    {
        var result = new SavedPlayerState();
        Body body = netBody.body;

        int itemKey = 0;
        for (int slot = 0; slot < body.slots.Length; ++slot)
        {
            if (!body.HoldingItem(slot))
                continue;

            Item item = body.GetItem(slot);

            result.SavedItemList.Add(new SavedItem
            {
                id = item.id,
                condition = item.condition,
                slot = slot,
                favourited = item.favourited
            });

            result.ComponentsDictionary.Add(itemKey++, SerializeComponents(item));

            if (!item.GetComponent<Container>())
                continue;
            foreach (Component child in item.transform)
            {
                if (!child.TryGetComponent(out Item innerItem))
                    continue;

                result.SavedItemList.Add(new SavedItem
                {
                    id = innerItem.id,
                    condition = innerItem.condition,
                    slot = slot,
                    favourited = innerItem.favourited
                });

                result.ComponentsDictionary.Add(itemKey++, SerializeComponents(innerItem));
            }
        }

        foreach (Item wearable in body.GetAllWearables())
        {
            result.SavedItemList.Add(new SavedItem
            {
                id = wearable.id,
                condition = wearable.condition,
                slot = -1,
                wearSlot = wearable.Stats.wearSlotId,
                favourited = wearable.favourited
            });

            result.ComponentsDictionary.Add(itemKey++, SerializeComponents(wearable));

            if (!wearable.GetComponent<Container>())
                continue;
            foreach (Component child in wearable.transform)
            {
                if (!child.TryGetComponent(out Item innerItem))
                    continue;

                result.SavedItemList.Add(new SavedItem
                {
                    id = innerItem.id,
                    condition = innerItem.condition,
                    slot = -1,
                    wearSlot = wearable.Stats.wearSlotId,
                    favourited = innerItem.favourited
                });

                result.ComponentsDictionary.Add(itemKey++, SerializeComponents(innerItem));
            }
        }

        result.BodyComponentsDictionaryUnused.Add(SerializeComponents(body));

        if (body.TryGetComponent(out Painkillers _))
        {
            result.Painkillers = new CharacterHealthPainkillerStateSyncPacket(body);
        }

        result.Health = new CharacterHealthStateSyncPacket(body);

        result.RecipesCrafted = new List<int>(netBody.plr.tosave_hascrafterbeforerecipes);

        return result;
    }

    public void Apply(NetBody netBody, RestoreSelection selection)
    {
        var body = netBody.body;

        if (selection.HasFlag(RestoreSelection.Crafting))
        {
            netBody.plr.tosave_hascrafterbeforerecipes = RecipesCrafted;
        }

        if (selection.HasFlag(RestoreSelection.Health))
        {
            Health.Apply(body);

            Painkillers.Apply(body);
        }

        if (selection.HasFlag(RestoreSelection.Inventory))
        {
            foreach (Item item in body.GetAllItemsThorough())
                Object.Destroy(item.gameObject);
            body.Body_DropAllItems();

            for (int i = 0; i < SavedItemList.Count; i++)
            {
                var savedItem = SavedItemList[i];

                Item item;
                try
                {
                    var gameObject = Object.Instantiate(Resources.Load(savedItem.id), body.transform.position + (Vector3)UnityEngine.Random.insideUnitCircle, Quaternion.identity) as GameObject;
                    item = gameObject.GetComponent<Item>();
                    item.condition = savedItem.condition;
                    // Drop favourited info
                    //item.favourited = savedItem.favourited;
                }
                catch (Exception ex)
                {
                    Plugin.PrintWarning($"Error occured while creating item \"{savedItem.id}\".\n{ex.Message}\n{ex.StackTrace}\nLoading will continue.");
                    continue;
                }

                try
                {
                    if (savedItem.slot >= 0)
                    {
                        if (body.HoldingItem(savedItem.slot))
                            body.GetItem(savedItem.slot).GetComponent<Container>().LoadItem(item);
                        else
                            body.PickUpItem(item, savedItem.slot, true);
                    }
                    else if (body.GetWearableBySlotID(savedItem.wearSlot))
                        body.GetWearableBySlotID(savedItem.wearSlot).GetComponent<Container>().LoadItem(item);
                    else
                        body.WearWearable(item);
                }
                catch (Exception ex)
                {
                    Plugin.PrintWarning($"Error occured while picking up item \"{item}\".\n{ex.Message}\n{ex.StackTrace}\nLoading will continue.");
                    continue;
                }

                var components = ComponentsDictionary[i];

                foreach (var (type, serialized) in components.Select(x => (x.Key, x.Value)))
                {
                    if (!item.gameObject.TryGetComponent(type, out var comp))
                        comp = item.gameObject.AddComponent(type);

                    var o = JObject.Parse(serialized);
                    foreach (var pair in o)
                    {
                        try
                        {
                            var field = type.GetField(pair.Key);
                            var value = pair.Value.ToObject(field.FieldType);
                            // Drop favourited info
                            if (field.Name.Contains("WasFavourited"))
                                value = false;
                            field.SetValue(comp, value);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PrintWarning($"Failed to deserialize component \"{comp}\" of type {type} in item \"{item}\".\nField: {pair.Key}\nValue:{pair.Value}\n{ex.Message}\n{ex.StackTrace}\nLoading will continue.");
                        }
                    }
                }
            }

            foreach (Item obj in body.GetAllItemsThorough())
            {
                if (NetObjectRegistry.ObjectCanBeIgnoredForNetwork(obj.gameObject))
                    continue;

                SyncInfo si = NetObjectRegistry.Server_EnsureItemIsNetworkRegistered(obj.gameObject);
                if (si != null)
                    NetObjectRegistry.Server_QueueSync(si);
            }
        }

        if (selection.HasFlag(RestoreSelection.Health))
        {
            MedicalSync.Server_QueueSendCharacterHealth(netBody, true);
        }
    }

    public class SavedPlayerStateContractResolver : DefaultContractResolver
    {
        private static readonly HashSet<Type> MpModDataStructs = [
            typeof(CharacterHealthStateSyncPacket), typeof(CharacterLimbHealthState), typeof(CharacterSkillsStateSyncPacket),
            typeof(CharacterHealthPainkillerStateSyncPacket)
        ];

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            var members = new List<MemberInfo>();

            const BindingFlags allInstance =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            if (MpModDataStructs.Contains(objectType))
            {
                members.AddRange(objectType
                    .GetFields(allInstance)
                    .Where(f => f.IsPublic || MpModDataStructs.Contains(f.FieldType)));

                members.AddRange(objectType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                members.AddRange(objectType.GetFields(allInstance));
            }

            return members;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            property.Readable = true;
            property.Writable = true;
            return property;
        }
    }
}
