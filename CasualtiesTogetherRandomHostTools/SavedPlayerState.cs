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

public sealed class SavedPlayerState
{
    public List<SavedItem> SavedItemList = [];
    public Dictionary<int, Dictionary<Type, string>> ItemComponentsDictionary = [];
    public Dictionary<Type, string> BodyComponentsDictionary = [];
    public List<Dictionary<Type, string>> LimbComponentsDictionary = [];
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

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented, JsonSettings);

    public static SavedPlayerState Deserialize(string str) => JsonConvert.DeserializeObject<SavedPlayerState>(str, JsonSettings);

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

            result.ItemComponentsDictionary.Add(itemKey++, SerializeComponents(item));

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

                result.ItemComponentsDictionary.Add(itemKey++, SerializeComponents(innerItem));
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

            result.ItemComponentsDictionary.Add(itemKey++, SerializeComponents(wearable));

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

                result.ItemComponentsDictionary.Add(itemKey++, SerializeComponents(innerItem));
            }
        }

        result.BodyComponentsDictionary = SerializeComponents(body);

        foreach (var limb in body.limbs)
            result.LimbComponentsDictionary.Add(SerializeComponents(limb));

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

            DeserializeComponents(body.gameObject, BodyComponentsDictionary);

            for (int i = 0; i < body.limbs.Length; i++)
                DeserializeComponents(body.limbs[i].gameObject, LimbComponentsDictionary[i]);
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

                DeserializeComponents(item.gameObject, ItemComponentsDictionary[i]);
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

    private static Dictionary<Type, string> SerializeComponents(Component item)
    {
        return item.gameObject.GetComponents<Component>()
            .Where(x => Attribute.GetCustomAttributes(x.GetType()).Any(attr => attr is Saveable))
            .ToDictionary(component => component.GetType(), component => JsonConvert.SerializeObject(component));
    }

    private static void DeserializeComponents(GameObject parent, Dictionary<Type, string> serializedComponents)
    {
        foreach (var (type, serialized) in serializedComponents.Select(x => (x.Key, x.Value)))
        {
            if (!parent.gameObject.TryGetComponent(type, out var comp))
                comp = parent.gameObject.AddComponent(type);

            JObject o;
            try
            {
                o = JObject.Parse(serialized);
            }
            catch (Exception ex)
            {
                Plugin.PrintWarning($"Failed to deserialize component \"{type.Name}\" of GameObject \"{parent.name}\".\n{ex.Message}\n{ex.StackTrace}\nLoading of other components will continue.");
                return;
            }

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
                    Plugin.PrintWarning($"Failed to update component \"{type.Name}\" of GameObject \"{parent.name}\".\nField: {pair.Key}\nValue:{pair.Value}\n{ex.Message}\n{ex.StackTrace}\nLoading of other fields will continue.");
                }
            }
        }
    }
}
