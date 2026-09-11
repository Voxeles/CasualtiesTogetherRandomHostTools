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

    // Issues:
    // Mp mod has no way of syncing favourited items. Drop this info so that clients can still craft.
    // Mp mod has no way of syncing crafted recipes. This info is still kept server-side but never communicated to the clients map upon load

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
                //favourited = item.favourited
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
                    //favourited = innerItem.favourited
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
                //favourited = wearable.favourited
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
                    //favourited = innerItem.favourited
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

    public void Apply(NetBody netBody)
    {
        var body = netBody.body;

        Plugin.Logger.LogInfo($"Destroying items");

        foreach (Item item in body.GetAllItemsThorough())
            Object.Destroy(item.gameObject);
        body.Body_DropAllItems();

        Plugin.Logger.LogInfo($"tosave_hascrafterbeforerecipes");

        netBody.plr.tosave_hascrafterbeforerecipes = RecipesCrafted;

        Plugin.Logger.LogInfo($"Health");

        Health.Apply(body);

        Plugin.Logger.LogInfo($"Painkillers");

        Painkillers.Apply(body);

        Plugin.Logger.LogInfo($"Items");

        for (int i = 0; i < SavedItemList.Count; i++)
        {
            var savedItem = SavedItemList[i];

            Plugin.Logger.LogInfo($"Item {i}: {savedItem.id}");

            Item item;
            try
            {
                var gameObject = Object.Instantiate(Resources.Load(savedItem.id), body.transform.position + (Vector3)UnityEngine.Random.insideUnitCircle, Quaternion.identity) as GameObject;
                item = gameObject.GetComponent<Item>();
                item.condition = savedItem.condition;
                item.favourited = savedItem.favourited;
            }
            catch (Exception ex)
            {
                ConsoleScript.instance.Alert($"Error occured during creating item \"{savedItem.id}\".\n{ex.Message}\n{ex.StackTrace}\nLoading will continue.");
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
                ConsoleScript.instance.Alert($"Error occured during picking up item \"{item}\".\n{ex.Message}\n{ex.StackTrace}\nLoading will continue.");
                continue;
            }

            Plugin.Logger.LogInfo($"Components");

            var components = ComponentsDictionary[i];

            foreach (var (type, serialized) in components.Select(x => (x.Key, x.Value)))
            {
                Plugin.Logger.LogInfo($"Component {type}: {serialized}");

                if (!item.gameObject.TryGetComponent(type, out var comp))
                    comp = item.gameObject.AddComponent(type);

                var o = JObject.Parse(serialized);
                foreach (var pair in o)
                {
                    try
                    {
                        var field = type.GetField(pair.Key);
                        var value = pair.Value.ToObject(field.FieldType);
                        field.SetValue(comp, value);
                        Plugin.Logger.LogInfo($"SET Component {comp} field {field} to {value}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to deserialize {type} in {item} at {pair.Key} with {pair.Value}: {ex}");
                    }
                }
            }
        }

        // Apply body components...? I think it's just painkillers and mindwipe, both are already handled by MP mod's packets

        foreach (Item obj in body.GetAllItemsThorough())
        {
            if (NetObjectRegistry.ObjectCanBeIgnoredForNetwork(obj.gameObject))
                continue;

            SyncInfo si = NetObjectRegistry.Server_EnsureItemIsNetworkRegistered(obj.gameObject);
            if (si != null)
                NetObjectRegistry.Server_QueueSync(si);
        }

        MedicalSync.Server_QueueSendCharacterHealth(netBody, true);
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
