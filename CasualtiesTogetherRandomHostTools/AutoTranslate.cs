using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using KrokoshaCasualtiesMP;
using Newtonsoft.Json;

namespace CasualtiesTogetherRandomHostTools;

public class AutoTranslate
{
    public ConfigEntry<bool> ConfigAutoTranslateEnabled;
    public ConfigEntry<string> ConfigAutoTranslateUrl;
    public ConfigEntry<string> ConfigAutoTranslateKey;
    public ConfigEntry<string> ConfigAutoTranslateTargetLang;
    public HashSet<NetPlayer> DoNotTranslateList = [];
    public bool ForceDisableWhenKaizo = false;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5.0) };
    
    public static AutoTranslate Instance;
    
    public AutoTranslate(Plugin plugin)
    {
        if (Instance != null)
            return;
        NetPlayer.OnPlayerLeft += OnPlayerLeft;
        Instance = this;
        ConfigAutoTranslateEnabled = plugin.Config.Bind(
            "AutoTranslate",
            "Enabled",
            false,
            "Enable player chat auto translation");
        ConfigAutoTranslateUrl = plugin.Config.Bind(
            "AutoTranslate",
            "DeeplUrl",
            "https://api-free.deepl.com/v2/translate",
            "Deepl API URL");
        ConfigAutoTranslateKey = plugin.Config.Bind(
            "AutoTranslate",
            "DeeplKey",
            "",
            "Deepl API key, required");
        ConfigAutoTranslateTargetLang = plugin.Config.Bind(
            "AutoTranslate",
            "TargetLang",
            "ES",
            "Target language, see: https://developers.deepl.com/docs/getting-started/supported-languages");
    }

    public bool ShouldTranslate(NetPlayer player)
    {
        if (!ConfigAutoTranslateEnabled.Value)
            return false;
        return !DoNotTranslateList.Contains(player);
    }

    public async Task<string> TranslateAsync(string text) 
        => await TranslateAsync(text, ConfigAutoTranslateTargetLang.Value);
    
    public async Task<string> TranslateAsync(string text, string targetLang)
    {
        if (string.IsNullOrEmpty(text)
            || string.IsNullOrEmpty(targetLang)
            || string.IsNullOrEmpty(ConfigAutoTranslateKey.Value))
            return null;

        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("DeepL-Auth-Key", ConfigAutoTranslateKey.Value);
        
        var formData = new List<KeyValuePair<string, string>>
        {
            new("text", text),
            new("target_lang", targetLang)
        };
        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(ConfigAutoTranslateUrl.Value, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Con.con.LogToConsole($"ERROR: AutoTranslate Deepl API error: {response.StatusCode}: {responseBody}");
            Plugin.Logger.LogError($"Deepl API error {response.StatusCode}: {responseBody}");
        }
        
        var result = JsonConvert.DeserializeObject<DeepLResponse>(responseBody);
        return result?.Translations?[0]?.Text;
    }

    private static void OnPlayerLeft(NetPlayer player)
    {
        Instance?.DoNotTranslateList.Remove(player);
    }
    
    private class DeepLResponse
    {
        [JsonProperty("translations")]
        public List<DeepLTranslation> Translations { get; set; }
    }
    
    private class DeepLTranslation
    {
        [JsonProperty("detected_source_language")]
        public string DetectedSourceLanguage { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

}