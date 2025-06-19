using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using UnityEngine;

public class TextTranslation : MonoBehaviour
{
    [SerializeField] private string googleTranslateApiKey;
    [SerializeField] private string targetLanguage = "en";

    private HttpClient _httpClient;
    private bool debug = true; // Set to false in production

    private void Awake()
    {
        _httpClient = new HttpClient();
    }

    private void OnDestroy()
    {
        _httpClient.Dispose();
    }

    public async Task<string> TranslateTextAsync(string text, string sourceLanguage)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        try
        {
            // Create request object WITHOUT source parameter if it's auto/unknown
            object requestJson;

            if (string.IsNullOrEmpty(sourceLanguage) || sourceLanguage == "unknown" || sourceLanguage == "auto")
            {
                // Omit source parameter to let Google auto-detect the language
                requestJson = new
                {
                    q = text,
                    target = targetLanguage,
                    format = "text"
                };

                if (debug)
                {
                    Debug.Log("<<<< [TextTranslator] Using auto-detection (omitting source parameter)");
                }
            }
            else
            {
                requestJson = new
                {
                    q = text,
                    source = sourceLanguage,
                    target = targetLanguage,
                    format = "text"
                };

                if (debug)
                {
                    Debug.Log($"<<<< [TextTranslator] Using source language: {sourceLanguage}");
                }
            }

            string jsonString = JsonConvert.SerializeObject(requestJson);
            if (debug)
            {
                Debug.Log($"[TextTranslator] Request JSON: {jsonString}");
            }

            var content = new StringContent(
                jsonString,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"https://translation.googleapis.com/language/translate/v2?key={googleTranslateApiKey}",
                content);

            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[TextTranslator] Google Translate API error: {response.StatusCode}, {responseJson}");
                return text; // Return original text on error
            }

            if (debug)
            {
                Debug.Log($"<<<<< [TextTranslator] Response: {responseJson}");
            }

            var translationResponse = JsonConvert.DeserializeObject<TranslateResponse>(responseJson);

            if (translationResponse?.data?.translations == null || translationResponse.data.translations.Length == 0)
                return text;

            string translatedText = translationResponse.data.translations[0].translatedText;

            if (debug)
            {
                Debug.Log($"<<<<<<<<< [TextTranslator] Translated text: '{text}' -> '{translatedText}'");

                if (!string.IsNullOrEmpty(translationResponse.data.translations[0].detectedSourceLanguage))
                {
                    Debug.Log($"<<<<<<<<< [TextTranslator] Detected source language: {translationResponse.data.translations[0].detectedSourceLanguage}");
                }
            }

            return translatedText;
        }
        catch (Exception ex)
        {
            Debug.LogError($"<<<<<< [TextTranslator] Error translating text: {ex.Message}");
            return text; // Return original text on error
        }
    }

    // Helper classes for JSON deserialization
    [Serializable]
    private class TranslateResponse
    {
        public TranslationData data;
    }

    [Serializable]
    private class TranslationData
    {
        public Translation[] translations;
    }

    [Serializable]
    private class Translation
    {
        public string translatedText;
        public string detectedSourceLanguage;
    }
}
