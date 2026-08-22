using System;
using System.Collections.Generic;
using System.Net;
using BepInEx.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KrokoshaCasualtiesMP;
using Newtonsoft.Json;

namespace CasualtiesTogetherRandomHostTools;

public class AutoTranslate
{
    public ConfigEntry<bool> ConfigAutoTranslateEnabled;
    public ConfigEntry<string> ConfigAutoTranslateBackend;
    public ConfigEntry<string> ConfigAutoTranslateAuthHeader;
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
        ConfigAutoTranslateBackend = plugin.Config.Bind(
            "AutoTranslate",
            "Backend",
            "GoogleTranslate-Free",
            "Auto translation backend\nPossible values: GoogleTranslate-Free, DeepL-API (requires AuthHeader)");
        ConfigAutoTranslateAuthHeader = plugin.Config.Bind(
            "AutoTranslate",
            "AuthHeader",
            "",
            "Authentication header (required for some backends)\nFormatted as: <auth scheme> <parameter>");
        ConfigAutoTranslateTargetLang = plugin.Config.Bind(
            "AutoTranslate",
            "TargetLang",
            "es",
            "Target language\nFor Google translate, see: https://docs.cloud.google.com/translate/docs/languages\nFor DeepL, see: https://developers.deepl.com/docs/getting-started/supported-languages");
    }

    public bool ShouldTranslate(NetPlayer player)
    {
        if (!ConfigAutoTranslateEnabled.Value)
            return false;
        return !DoNotTranslateList.Contains(player);
    }

    public async Task<string> TranslateAsync(string text) 
        => await TranslateAsync(text, ConfigAutoTranslateTargetLang.Value);
    
    public async Task<string> TranslateAsync(string text, Language language) 
        => await TranslateAsync(text, GetTargetLangCode(language));
    
    public async Task<string> TranslateAsync(string text, string targetLang)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(targetLang))
            return null;
        
        switch (ConfigAutoTranslateBackend.Value)
        {
            case "GoogleTranslate-Free": return await TranslateGoogleTranslateFreeAsync(text, targetLang);
            case "DeepL-API": return await TranslateDeeplApiAsync(text, targetLang);
            default:
                Plugin.PrintError($"Unsupported backend \"{ConfigAutoTranslateBackend.Value}\"!");
                return null;
        }
    }
    
    // Not exhaustive obviously, just used for the Kaizo mode
    public enum Language
    {
        Chinese, Finnish, Czech, Dutch, Portuguese
    }

    public string GetTargetLangCode(Language language)
    {
        switch (ConfigAutoTranslateBackend.Value)
        {
            case "GoogleTranslate-Free":
                return TargetLangGoogleTranslateFree(language);
            case "DeepL-API":
                return TargetLangDeeplApi(language);
            default:
                Plugin.PrintError($"Unsupported backend \"{ConfigAutoTranslateBackend.Value}\"!");
                return null;
        }
    }
    
    private async Task<string> TranslateGoogleTranslateFreeAsync(string text, string targetLang)
    {
        const string backendName = "AutoTranslate GoogleTranslate-Free";

        try
        {
            var url = $"https://translate.google.com/m?ie=UTF-8&hl=en&sl=auto&tl={Uri.EscapeDataString(targetLang)}&q={Uri.EscapeDataString(text.Trim())}&op=translate";
            using var response = await _httpClient.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Plugin.PrintError($"{backendName} error: {response.StatusCode}");
                Plugin.Logger.LogInfo($"{backendName} HTML response: {html}");
                return null;
            }
            
            var match = Regex.Match(html, """class="result-container">([^<]*)</div>""", RegexOptions.Multiline);

            if (!match.Success)
            {
                Plugin.PrintError($"{backendName} error: could not find match!");
                Plugin.Logger.LogInfo($"{backendName} HTML response: {html}");
                return null;
            }
            
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        catch (Exception ex)
        {
            Plugin.PrintError($"{backendName} exception: " + ex);
            return null;
        }
    }

    private async Task<string> TranslateDeeplApiAsync(string text, string targetLang)
    {
        const string backendName = "AutoTranslate DeepL-API";

        if (string.IsNullOrEmpty(ConfigAutoTranslateAuthHeader.Value))
        {
            Plugin.PrintError($"{backendName} backend requires an authentication header!");
            return null;
        }

        try
        {
            var split = ConfigAutoTranslateAuthHeader.Value.Split([' '], 2);
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(split[0], split[1]);

            var url = split[1].EndsWith(":fx")
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";
        
            var formData = new List<KeyValuePair<string, string>>
            {
                new("text", text),
                new("target_lang", targetLang)
            };
            using var content = new FormUrlEncodedContent(formData);
            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Plugin.PrintError($"{backendName} API error: {response.StatusCode}: {responseBody}");
                return null;
            }
        
            var result = JsonConvert.DeserializeObject<DeepLResponse>(responseBody);
            return result?.Translations?[0]?.Text;
        }
        catch (Exception ex)
        {
            Plugin.PrintError($"{backendName} exception: " + ex);
            return null;
        }
    }

    private static string TargetLangGoogleTranslateFree(Language language)
    {
        return language switch
        {
            Language.Chinese => "zh-CN",
            Language.Finnish => "fi",
            Language.Czech => "cs",
            Language.Dutch => "nl",
            Language.Portuguese => "pt",
            _ => null,
        };
    }
    
    private static string TargetLangDeeplApi(Language language)
    {
        return language switch
        {
            Language.Chinese => "ZH",
            Language.Finnish => "FI",
            Language.Czech => "CS",
            Language.Dutch => "NL",
            Language.Portuguese => "PT",
            _ => null,
        };
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