using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using PassthroughCameraSamples;
using UnityEngine.UI;
using System.Collections;
using static OVRPlugin.Qpl;
using NUnit.Framework.Internal;

public class TextDetection : MonoBehaviour
{
    [SerializeField] private int sampleFactor = 2;
    [SerializeField] private ComputeShader downsampleShader;
    [SerializeField] private string googleVisionApiKey;

    // Can be used to test the functionality in the editor
    //[SerializeField] private TextTranslation textTranslation;
    //[SerializeField] private Texture2D testTexture;

    private RenderTexture downsampledTexture;

    private bool isProcessing;

    private HttpClient httpClient;

    private static readonly int Input1 = Shader.PropertyToID("_Input");
    private static readonly int Output = Shader.PropertyToID("_Output");
    private static readonly int InputWidth = Shader.PropertyToID("_InputWidth");
    private static readonly int InputHeight = Shader.PropertyToID("_InputHeight");
    private static readonly int OutputWidth = Shader.PropertyToID("_OutputWidth");
    private static readonly int OutputHeight = Shader.PropertyToID("_OutputHeight");

    public int SampleFactor => sampleFactor;

    private void Awake()
    {
        httpClient = new HttpClient();
    }

    // Can be used to test the functionality in the editor
    //private async Task Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        var textResults = await ScanFrameAsync(testTexture);
    //        Debug.Log("<<<<[TextDetectionScanner] language >>>>" + textResults[0].languageCode);
    //        string translatedText = await textTranslation.TranslateTextAsync(textResults[0].text, textResults[0].languageCode);

    //        foreach (var result in textResults)
    //        {
    //            Debug.Log($"<<<<[TextDetectionScanner] Detected text: {result.text}");
    //            Debug.Log($"<<<<[TextDetectionScanner] Bounding box: {string.Join(", ", result.boundingBox)}");
    //        }
    //    }
    //}

    public async Task<TextDetectionResult[]> ScanFrameAsync(Texture2D texture)
    {
        if (isProcessing)
            return null;

        Debug.Log("<<<<[TextDetectionScanner] Scanning frame >>>>");
        isProcessing = true;
        try
        {
            // Downsample the texture for performance
            var originalWidth = texture.width;
            var originalHeight = texture.height;
            var targetWidth = Mathf.Max(1, originalWidth / sampleFactor);
            var targetHeight = Mathf.Max(1, originalHeight / sampleFactor);
            //Debug.Log($"<<<<[TextDetectionScanner] Downsampling texture from {originalWidth}x{originalHeight} to {targetWidth}x{targetHeight} >>>>");

            if (!downsampledTexture || downsampledTexture.width != targetWidth || downsampledTexture.height != targetHeight)
            {
                Debug.Log("<<<<[TextDetectionScanner] Creating new downsampled texture >>>>");
                if (downsampledTexture)
                {
                    downsampledTexture.Release();
                }

                downsampledTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32)
                {
                    enableRandomWrite = true
                };

                downsampledTexture.Create();
            }

            var kernel = downsampleShader.FindKernel("CSMain");
            downsampleShader.SetTexture(kernel, Input1, texture);
            downsampleShader.SetTexture(kernel, Output, downsampledTexture);
            downsampleShader.SetInt(InputWidth, originalWidth);
            downsampleShader.SetInt(InputHeight, originalHeight);
            downsampleShader.SetInt(OutputWidth, targetWidth);
            downsampleShader.SetInt(OutputHeight, targetHeight);

            Debug.Log("<<<<[TextDetectionScanner] Dispatching downsample shader >>>>");

            var threadGroupsX = Mathf.CeilToInt(targetWidth / 8f);
            var threadGroupsY = Mathf.CeilToInt(targetHeight / 8f);
            downsampleShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

            // Create a readable texture from the render texture
            var readableTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            RenderTexture.active = downsampledTexture;
            readableTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = null;

            Debug.Log("<<<<[TextDetectionScanner] Texture read >>>>");

            // Convert to base64 for API
            byte[] jpgBytes = readableTexture.EncodeToJPG(75);
            string base64Image = Convert.ToBase64String(jpgBytes);

            //Destroy(readableTexture);
            Debug.Log("<<<<[TextDetectionScanner] Calling function to send img to Google Vision API >>>>");
            // Send to Google Vision API
            return await SendToGoogleVisionAsync(base64Image);
        }
        finally
        {
            isProcessing = false;
        }
    }

    private async Task<TextDetectionResult[]> SendToGoogleVisionAsync(string base64Image)
    {
        try
        {
            // Create Vision API request
            var visionRequest = new
            {
                requests = new[]
                {
                    new
                    {
                        image = new { content = base64Image },
                        features = new[]
                        {
                            new { type = "TEXT_DETECTION" },
                        }
                    }
                }
            };

            Debug.Log("<<<<<< [TextDetectionScanner] Sending request to Google Vision API >>>>");

            var json = JsonConvert.SerializeObject(visionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://vision.googleapis.com/v1/images:annotate?key={googleVisionApiKey}",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.LogError($"<<<<<< [TextDetectionScanner] Google Vision API error: {response.StatusCode}, {errorContent}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<GoogleVisionResponse>(responseJson);
            Debug.Log("<<<<<< [TextDetectionScanner] Received response from Google Vision API >>>>");

            if (responseData?.responses == null || responseData.responses.Length == 0 ||
                (responseData.responses[0].textAnnotations == null && responseData.responses[0].fullTextAnnotation == null))
            {
                return null;
            }

            var results = new List<TextDetectionResult>();

            Debug.Log("<<<<<< [TextDetectionScanner] Parsing response >>>>");

            foreach (var annotation in responseData.responses[0].textAnnotations)
            {
                if (annotation.boundingPoly?.vertices == null || annotation.boundingPoly.vertices.Length < 4)
                    continue;
                Debug.Log("<<<<<< [TextDetectionScanner] Responce Text annoation >>>>" + annotation.description);

                var boundingBox = new Vector2[annotation.boundingPoly.vertices.Length];
                for (int i = 0; i < annotation.boundingPoly.vertices.Length; i++)
                {
                    var vertex = annotation.boundingPoly.vertices[i];
                    //boundingBox[i] = new Vector2(
                    //    vertex.x / (float)width,
                    //    vertex.y / (float)height
                    //);
                    boundingBox[i] = new Vector2(
                        vertex.x,
                        vertex.y
                    );
                }

                results.Add(new TextDetectionResult
                {
                    text = annotation.description,
                    boundingBox = boundingBox,
                    languageCode = annotation.locale ?? "unknown"
                });
            }
            return results.ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogError($"<<<< [TextDetectionScanner] Error communicating with Google Vision API: {ex.Message}");
            return null;
        }
    }

    private void OnDestroy()
    {
        if (downsampledTexture != null)
        {
            Debug.Log("<<<< [TextDetectionScanner] Destroying downsampled texture >>>>");
            downsampledTexture.Release();
            Destroy(downsampledTexture);
        }
        httpClient.Dispose();
    }

    #region Helper classes for JSON deserialization

    [Serializable]
    public class TextDetectionResult
    {
        public string text;
        public Vector2[] boundingBox;
        public string languageCode;
    }

    [Serializable]
    private class GoogleVisionResponse
    {
        public VisionResponseItem[] responses;
    }

    [Serializable]
    private class VisionResponseItem
    {
        public TextAnnotation[] textAnnotations;
        public FullTextAnnotation fullTextAnnotation;
    }

    [Serializable]
    private class TextAnnotation
    {
        public string description;
        public string locale;
        public BoundingPoly boundingPoly;
    }

    [Serializable]
    private class FullTextAnnotation
    {
        public string text;
        public Page[] pages;
    }

    [Serializable]
    private class Page
    {
        public Block[] blocks;
    }

    [Serializable]
    private class Block
    {
        public Paragraph[] paragraphs;
    }

    [Serializable]
    private class Paragraph
    {
        public Word[] words;
    }

    [Serializable]
    private class Word
    {
        public Symbol[] symbols;
    }

    [Serializable]
    private class Symbol
    {
        public string text;
    }

    [Serializable]
    private class BoundingPoly
    {
        public Vertex[] vertices;
    }

    [Serializable]
    private class Vertex
    {
        public int x;
        public int y;
    }

    #endregion Helper classes for JSON deserialization
}
