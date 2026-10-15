using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

[HarmonyPatch(typeof(CustomItemBehaviour), "Update")]
internal static class CloneJetpackPatch
{
    // Run the vanilla jetpack code for clones, but without the fuel drain since the MP mod already does that
    private static void Postfix(CustomItemBehaviour __instance, bool __runOriginal)
    {
        if (__runOriginal || !Net.IsServer || !CloneManager.Any)
            return;

        var item = __instance.item;
        if (!item || item.id != "jetpack")
            return;

        var parent = item.transform.parent;
        if (!parent || !parent.TryGetComponent<Limb>(out var limb) || !CloneManager.TryGet(limb.body, out var clone) || !clone.Ready)
            return;

        var body = limb.body;
        var flame = __instance.transform.GetChild(0).GetComponent<SpriteRenderer>();
        var audio = __instance.GetComponent<AudioSource>();
        __instance.data ??= [0f];

        float sputter = Mathf.MoveTowards((float)__instance.data[0], 0f, Time.deltaTime);
        if (sputter == 0f && item.isWet)
            sputter = Random.Range(-1f, 0.7f);

        bool thrusting = CloneInput.IsFresh && CloneInput.Move.y > 0f;
        if (item.condition > 0f && sputter <= 0f && body.conscious && !body.currentClimbable && thrusting)
        {
            if (!body.standing)
            {
                limb.rb.MoveRotation(Mathf.LerpAngle(limb.transform.eulerAngles.z, 0f, Time.deltaTime * 15f));
                limb.rb.AddForce(limb.transform.up * 198000f * Time.deltaTime + Vector3.right * 60000f * body.moveDir.x * Time.deltaTime);
            }
            else
            {
                body.rb.AddForce(Vector3.up * 198000f * Time.deltaTime + Vector3.right * 60000f * body.moveDir.x * Time.deltaTime);
            }

            flame.color = new Color(1f, 1f, 1f, Random.value);
            audio.volume = Mathf.MoveTowards(audio.volume, 1f, Time.deltaTime * 3f);
            audio.pitch = Mathf.MoveTowards(audio.pitch, 1f, Time.deltaTime * 3f);
            if (!audio.isPlaying)
                audio.Play();
        }
        else
        {
            flame.color = Color.clear;
            audio.volume = Mathf.MoveTowards(audio.volume, 0f, Time.deltaTime * 5f);
            audio.pitch = Mathf.MoveTowards(audio.pitch, 0f, Time.deltaTime * 5f);
            if (audio.isPlaying && audio.volume <= 0f)
                audio.Stop();
        }
        __instance.data[0] = sputter;
    }
}
