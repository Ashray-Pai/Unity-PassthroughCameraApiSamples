using PassthroughCameraSamples;
using TMPro;
using UnityEngine;

public class TranslationMananger : MonoBehaviour
{
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private TextDetection textDetection;
    [SerializeField] private TextTranslation textTranslation;
    [SerializeField] private TextMeshProUGUI detectedTextDisplay;
    [SerializeField] private TextMeshProUGUI translatedTextDisplay;

    private Texture2D m_cameraSnapshot;
    private Color32[] m_pixelsBuffer;
    private bool isProcessing = false;

    private async void Update()
    {
        if (webCamTextureManager.WebCamTexture == null)
            return;

        if (OVRInput.GetDown(OVRInput.Touch.One))
        {
            if (!isProcessing)
            {
                isProcessing = true;

                // Asking the canvas to make a snapshot before stopping WebCamTexture
                MakeCameraSnapshot();
                webCamTextureManager.WebCamTexture.Stop();

                var textResults = await textDetection.ScanFrameAsync(m_cameraSnapshot);
                if (textResults != null)
                {
                    // Display detected text
                    detectedTextDisplay.text = textResults[0].text;
                    Debug.Log($"Detected text: {detectedTextDisplay.text}");

                    // Translate the detected text
                    var translatedText = await textTranslation.TranslateTextAsync(detectedTextDisplay.text, textResults[0].languageCode);
                    translatedTextDisplay.text = translatedText;
                    Debug.Log($"Translated text: {translatedTextDisplay.text}");
                    isProcessing = false;
                }
                else
                {
                    detectedTextDisplay.text = "No text detected.";
                    translatedTextDisplay.text = string.Empty;
                    isProcessing = false;
                }
            }
        }
    }

    public void MakeCameraSnapshot()
    {
        var webCamTexture = webCamTextureManager.WebCamTexture;
        if (webCamTexture == null || !webCamTexture.isPlaying)
            return;

        if (m_cameraSnapshot == null)
        {
            m_cameraSnapshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);
        }

        // Copy the last available image from WebCamTexture to a separate object
        m_pixelsBuffer ??= new Color32[webCamTexture.width * webCamTexture.height];
        _ = webCamTextureManager.WebCamTexture.GetPixels32(m_pixelsBuffer);
        m_cameraSnapshot.SetPixels32(m_pixelsBuffer);
        m_cameraSnapshot.Apply();
    }
}
