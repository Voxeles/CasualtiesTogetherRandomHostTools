using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
// - Applying the health sync packet triggers side effects, like bleeding for amputated limbs
// Ideally I'd use my own data structure for player data, or just aggregate all serializable fields in Body.cs and Limb.cs.
// But I'm lazy, so instead I'll set any fields that have a side effect manually before applying the health packet to the body.
// There are fields that need special handling anyway, so this isn't as unnecessary as it may seem.
// Additionally, I have to sync the health packet twice, since the client applying the incoming packets will also trigger any side effects.

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

        if (!body.gameObject.TryGetComponent<Painkillers>(out _))
            body.gameObject.AddComponent<Painkillers>();
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

        if (selection.HasFlag(RestoreSelection.Inventory))
        {
            // Destroy items now to prevent them from dropping as limbs are dismembered
            foreach (Item item in body.GetAllItemsThorough())
                Object.Destroy(item.gameObject);
            body.Body_DropAllItems();
        }

        if (selection.HasFlag(RestoreSelection.Health))
        {
            // We might have to modify this later, make a copy it
            // Seems bad for a method named 'Apply' to modify the stored data, even if it doesn't matter for our purposes
            var health = Health;

            // The mp mod seems to think 'one limb regrown' is 'all limbs regrown'
            // which, to be fair, is normally true
            // Regrow them all here to avoid regrowing limbs as we update their 'dismembered' status
            body.RegrowAllLimbs();

            var limbPackets = new[] {
                health.limb0, health.limb1, health.limb2, health.limb3, health.limb4,
                health.limb5, health.limb6, health.limb7, health.limb8, health.limb9,
                health.limb10, health.limb11, health.limb12, health.limb13, health.limb14
            };
            for (int i = 0; i < limbPackets.Length; i++)
            {
                var limbState = limbPackets[i];
                var limb = body.limbs[i];

                // All of these can trigger side effects in health.Apply(), so they need to be set beforehand
                limb.dismembered = limbState.dismembered;

                limb.dislocated = limbState.dislocationTimer > 0f;
                limb.dislocationTimer = limbState.dislocationTimer;

                limb.broken = limbState.boneHealTimer > 0f;
                limb.boneHealTimer = limbState.boneHealTimer;
            }

            // Avoid possible side effects from skills updating
            body.skills.STR = health.skills.skill_STR;
            body.skills.RES = health.skills.skill_RES;
            body.skills.INT = health.skills.skill_INT;
            body.skills.UpdateExpBoundaries();
            body.skills.expSTR = body.skills.minSTR;
            body.skills.expRES = body.skills.minRES;
            body.skills.expINT = body.skills.minINT;

            health.skills.skill_exp_STR = ValidateExp("STR", health.skills.skill_exp_STR, health.skills.skill_STR);
            health.skills.skill_exp_RES = ValidateExp("RES", health.skills.skill_exp_RES, health.skills.skill_RES);
            health.skills.skill_exp_INT = ValidateExp("INT", health.skills.skill_exp_INT, health.skills.skill_INT);

            // Will play the last stand animation if it's true, always set it to false
            // Duplicate last stands are prevented by 'triedRollingLastStand'
            // This field seems to be used for flavor text on the death stats screen anyway
            health.succesfullyRolledLastStand = false;

            // Game handles this setting in a pretty dumb way. If it's true, 'triedRollingLastStand' should always be false
            if (WorldGeneration.GetRunSettingBool("infinitelaststand"))
                body.triedRollingLastStand = health.triedRollingLastStand = false;
            else
                body.triedRollingLastStand = health.triedRollingLastStand;

            // Send a health sync now
            // Client-side, the MP mod will trigger some side effects mentioned above (we only prevented them server-side, after all),
            // but that's okay as we'll queue another sync later
            MedicalSync.Server_SendCharacterHealth(netBody, true);

            // health.Apply() will also modify some components
            // Apply the stored data first, then deserialize the components, then sync
            health.Apply(body);
            DeserializeComponents(body.gameObject, BodyComponentsDictionary);
            for (int i = 0; i < body.limbs.Length; i++)
                DeserializeComponents(body.limbs[i].gameObject, LimbComponentsDictionary[i]);

            // Queue a final sync
            MedicalSync.Server_QueueSendCharacterHealth(netBody, true);
        }

        if (selection.HasFlag(RestoreSelection.Inventory))
        {
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

            if (type == typeof(Painkillers))
            {
                // Destroy now if needed to prevent a small issue with the MP mod where the HealthUpdateSyncClients
                // coro can throw an exception due to the Painkillers being suddenly null
                var pnk = (Painkillers)comp;
                if (pnk.opiateAmount == 0f && pnk.opiateTolerance == 0f)
                    MonoBehaviour.Destroy(pnk);
            }
        }
    }

    private static ushort ValidateExp(string name, ushort exp, ushort level)
    {
        var lowerBound = Skills.GetExperienceForLevel(level);
        var upperBound = Skills.GetExperienceForLevel(level + 1);

        if (exp >= lowerBound && exp < upperBound)
            return exp;

        Plugin.PrintWarning(
            $"Experience amount for {name} is incorrect." +
            $"\n\tAt level {level}, the experience amount \"{exp}\" should be between \"{lowerBound}\" and \"{upperBound - 1}\"." +
            $"\n\tResetting {name} exp to the minimum amount for level {level}.");

        return lowerBound < 0 ? (ushort)0 : (ushort)lowerBound;
    }
}
