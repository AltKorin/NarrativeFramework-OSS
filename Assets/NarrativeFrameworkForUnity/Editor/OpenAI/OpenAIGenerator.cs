// Assets/SimulationGame/Editor/OpenAI/OpenAIGenerator.cs
// (Replace the whole file with this version)

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;                          // EditorPrefs fallback for the key
#endif
using Unity.Plastic.Newtonsoft.Json;

namespace SimulationGame.OpenAI
{
    /* ────────────────────────────────
     *  DTOs that mirror OpenAI schema
     * ──────────────────────────────── */
    [Serializable] public class OpenAIMessage      { public string role; public string content; }
    [Serializable] public class OpenAIChoice       { public int index;  public OpenAIMessage message; }
    [Serializable] public class OpenAIRawResponse  { public OpenAIChoice[] choices; }

    [Serializable] public class ImageApiData       { public string url; }
    [Serializable] public class ImageApiResponse   { public ImageApiData[] data; }

    /* ────────────────────────────────
     *  Generator – Chat & Image
     * ──────────────────────────────── */
    public static class OpenAIGenerator
    {
    #region Chat completion  ────────────────────────────────────────────────────
        /// <summary>Returns pure JSON string generated from <paramref name="prompt"/>.</summary>
        public static async Task<string> GenerateEvent(string prompt)
        {
            Debug.Log("<b>[OpenAI]</b> 🧠 Generating text …");

            /* 1. Load settings (asset is created automatically if missing). */
            OpenAISettings settings = OpenAISettings.LoadOrCreate();

            /* 2. Resolve API key – first from asset, then EditorPrefs as a fallback. */
            string apiKey = !string.IsNullOrEmpty(settings.apiKey)
                            ? settings.apiKey
                            : EditorPrefs.GetString("NarrativeFramework_OpenAIKey", "");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("<b>[OpenAI]</b> ⚠ API-key is missing (asset & EditorPrefs).");
                return null;
            }

            /* 3. Build request payload. */
            var payload = new
            {
                model        = settings.model,
                temperature  = settings.temperature,
                messages     = new[]
                {
                    // Inject system prompt only when provided
                    string.IsNullOrWhiteSpace(settings.systemPrompt)
                        ? null
                        : new { role = "system", content = settings.systemPrompt },
                    new { role = "user", content = prompt }
                }.Where(m => m != null),
                // Force JSON mode if requested
                response_format = settings.forceJsonOutput
                    ? new { type = "json_object" }
                    : null
            };

            string json = JsonConvert.SerializeObject(payload);

            /* 4. Fire request. */
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            try
            {
                var httpResp = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                string respText = await httpResp.Content.ReadAsStringAsync();
                Debug.Log($"<b>[OpenAI]</b> 📬 HTTP {httpResp.StatusCode}\n{respText}");

                if (!httpResp.IsSuccessStatusCode) return null;

                /* 5. Parse response. */
                var parsed = JsonConvert.DeserializeObject<OpenAIRawResponse>(respText);
                string raw = parsed?.choices?.FirstOrDefault()?.message?.content?.Trim();

                if (string.IsNullOrEmpty(raw))
                {
                    Debug.LogWarning("<b>[OpenAI]</b> ⚠ Empty answer.");
                    return null;
                }

                /* 6. If “forceJsonOutput” is OFF, strip any prose before the first brace. */
                if (!settings.forceJsonOutput)
                {
                    int idx = raw.IndexOf('{');
                    if (idx >= 0) raw = raw.Substring(idx);
                }

                if (settings.forceJsonOutput && !(raw.StartsWith("{") || raw.StartsWith("[")))
                {
                    Debug.LogWarning("<b>[OpenAI]</b> ⚠ Model did not return JSON despite being asked.");
                    return null;
                }

                Debug.Log($"<b>[OpenAI]</b> ✅ Received JSON ({Math.Min(raw.Length,300)} chars preview).");
                return raw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<b>[OpenAI]</b> ❌ Request failed: {ex.Message}");
                return null;
            }
        }
    #endregion

    #region Image generation  ───────────────────────────────────────────────────
        public static async Task<string> GenerateImage(string prompt)
        {
            Debug.Log("<b>[OpenAI]</b> 🖼 Generating image …");

            OpenAISettings settings = OpenAISettings.LoadOrCreate();
            string apiKey = !string.IsNullOrEmpty(settings.apiKey)
                            ? settings.apiKey
                            : EditorPrefs.GetString("NarrativeFramework_OpenAIKey", "");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("<b>[OpenAI]</b> ⚠ API-key missing – cannot generate image.");
                return null;
            }

            var payload = new
            {
                model   = "dall-e-3",
                prompt  = prompt,
                n       = 1,
                size    = "1024x1024",
                quality = "hd",
                response_format = "url"
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            try
            {
                var httpResp = await client.PostAsync(
                    "https://api.openai.com/v1/images/generations",
                    new StringContent(JsonConvert.SerializeObject(payload),
                                      Encoding.UTF8, "application/json"));

                string resp = await httpResp.Content.ReadAsStringAsync();

                if (!httpResp.IsSuccessStatusCode)
                {
                    Debug.LogError($"<b>[OpenAI]</b> ❌ Image API error {httpResp.StatusCode}\n{resp}");
                    return null;
                }

                var parsed = JsonConvert.DeserializeObject<ImageApiResponse>(resp);
                string url = parsed?.data?.FirstOrDefault()?.url;

                if (string.IsNullOrWhiteSpace(url))
                {
                    Debug.LogWarning("<b>[OpenAI]</b> ⚠ No URL in image response.");
                    return null;
                }

                Debug.Log($"<b>[OpenAI]</b> 🎯 Image URL:\n{url}");
                return url;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<b>[OpenAI]</b> ❌ Image request failed: {ex.Message}");
                return null;
            }
        }
    #endregion

    #region Assistants API stub  ────────────────────────────────────────────────
        /*
         * When settings.useAssistantApi == true you can call
         *   await GenerateViaAssistantAsync(prompt, settings);
         * leaving classic chat-completion untouched.
         *
         * For now we log a message so the project still compiles.
         */
        private static Task<string> GenerateViaAssistantAsync(string prompt, OpenAISettings s)
        {
            Debug.LogWarning("<b>[OpenAI]</b> Assistants API pathway not implemented yet.");
            return Task.FromResult<string>(null);
        }
    #endregion
    }
}
