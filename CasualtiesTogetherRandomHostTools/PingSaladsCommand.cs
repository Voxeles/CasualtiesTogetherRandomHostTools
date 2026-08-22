using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;
using TMPro;
using UnityEngine;

namespace CasualtiesTogetherRandomHostTools;

public static class PingSaladsCommand
{
    public static void Register()
    {
        var comm = new Command("PingSalads", "Show where the elder thornbacks are", args =>
        {
            ConsoleScript.instance.CheckForWorld();
            Con.ConFailIfCheatsDisabled();

            var duration = 6f;
            if (args.Length > 1)
            {
                duration = float.Parse(args[1]);
                if (duration < 0)
                    throw new Exception("Duration cannot be negative");
            }
            
            var salads = GameObject.FindObjectsByType<ElderThornbackBehaviour>(FindObjectsSortMode.None);
            if (salads == null || salads.Length == 0)
            {
                Con.con.LogToConsole("No elder thornbacks found.");
                return;
            }
            
            Con.con.LogToConsole($"Found {salads.Length} elder thornbacks.");

            foreach (var salad in salads)
            {
                if (salad.TryGetComponent<SaladTrackerComponent>(out var tracker))
                {
                    tracker.elapsed = 0f;
                    tracker.duration = duration;
                }
                else
                {
                    tracker = salad.gameObject.AddComponent<SaladTrackerComponent>();
                    tracker.salad = salad;
                    tracker.duration = duration;
                }
            }
            
        }, new Dictionary<int, List<string>> {
            {0, ["6"]}
        }, [
            ("duration", "optional, ping duration")
        ]);
        Con.RegisterCommand(comm);
    }
}

public class SaladTrackerComponent : MonoBehaviour
{
    public ElderThornbackBehaviour salad;
    public float duration = 6f;
    public float elapsed = 0f;
    private GameObject _gb;
    private SpriteRenderer _sr;
    private TextMeshPro _text; 

    private void Start()
    {
        _gb = new GameObject();
        _sr = _gb.AddComponent<SpriteRenderer>();
        _sr.sprite = KrokoshaCoopModAssets.arrowicon;
        _sr.color = new Color(0f, 1f, 0f, 1f);
        _sr.sortingOrder = 6010;
        _gb.transform.localScale = Vector3.one * 8f;
        var textGo = Instantiate(CharStatusVisuals.PREFAB_ExpTalkText, _gb.transform);
        textGo.SetActive(true);
        textGo.transform.localScale = Vector3.one / 8f;
        _text = textGo.GetComponent<TextMeshPro>();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed > duration)
        {
            Destroy(this);
            return;
        }
        
        var pos = PlayerCamera.main.body.transform.position;
        var dist = Vector3.Distance(pos, salad.transform.position);
        var dir = (pos - salad.transform.position).normalized;
        var angle = Vector2.SignedAngle(Vector2.right, dir) + 180f;

        _gb.transform.position = pos - dir * 5f;
        _gb.transform.eulerAngles = new Vector3(0f, 0f, angle);
        _text.transform.position = pos - dir * 8f;
        _text.transform.rotation = Quaternion.identity;
        _text.SetText($"{dist:F0}m");

        var t = elapsed / duration;
        var color = new Color(0f, 1f, 0f, Mathf.Lerp(1f, 0f, t * t));
        _sr.color = color;
        _text.color = color;
    }

    private void OnDestroy()
    {
        Destroy(_gb);
    }
}