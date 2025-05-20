using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Meta.XR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PassthroughCameraSamples;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class PoseDetectionOptimized : MonoBehaviour
{
    [Header("Webcam Settings")]
    [SerializeField] private WebCamTextureManager webcamTexture;

    [Header("Server Settings")]
    public string serverUrl = "http://127.0.0.1:5000/detect"; // Update with your Flask server URL

    [Header("Testing Settings")]
    public Texture2D testTexture;

    public bool useTestTexture = false;
    public Color keypointColor = Color.red;
    public int keypointSize = 5;

    private Texture2D snap;           // Reusable full-size texture
    private bool isSending = false;   // Flag to limit concurrent requests
    public RawImage finalOutput;

    [Header("Controls configuration")]
    [SerializeField] private OVRInput.RawButton m_actionButton = OVRInput.RawButton.A;

    private bool startDetection;

    private IEnumerator Start()
    {
        while (webcamTexture.WebCamTexture == null)
        {
            yield return null;
        }
        Debug.Log("<<<<<<  webcam has started >>>>>");
        // Initialize reusable snap texture
        snap = new Texture2D(webcamTexture.WebCamTexture.width, webcamTexture.WebCamTexture.height, TextureFormat.RGB24, false);
    }

    private void Update()
    {
        if (webcamTexture.WebCamTexture == null)
            return;

        if (OVRInput.GetUp(m_actionButton))
        {
            startDetection = !startDetection;
        }

        // Only proceed if webcam is ready, detection is active, and no request is in progress
        if (webcamTexture != null && webcamTexture.WebCamTexture.isPlaying && startDetection && !isSending)
        {
            isSending = true;
            Texture2D resized = GetTextureFromWebcam();
            StartCoroutine(SendImage(resized));
        }
    }

    private Texture2D GetTextureFromWebcam()
    {
        // Update snap with current webcam pixels
        snap.SetPixels(webcamTexture.WebCamTexture.GetPixels());
        snap.Apply();
        // Resize to 640x480 for efficiency
        return ResizeTexture(snap, 640, 480);
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = new RenderTexture(newWidth, newHeight, 24);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        rt.Release();
        return result;
    }

    private IEnumerator SendImage(Texture2D texture)
    {
        byte[] imageBytes = texture.EncodeToJPG();
        Debug.Log("Image Bytes Length: " + imageBytes.Length);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "image.jpg", "image/jpeg");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Response: " + request.downloadHandler.text);
            ProcessResponse(request.downloadHandler.text, texture);
        }
        else
        {
            Debug.LogError("Request Failed: " + request.error);
        }
        isSending = false; // Allow the next request
    }

    private void ProcessResponse(string jsonResponse, Texture2D texture)
    {
        try
        {
            JObject response = JObject.Parse(jsonResponse);

            if (response.ContainsKey("keypoints"))
            {
                JArray keypoints = (JArray)response["keypoints"];
                Debug.Log($"Keypoints Detected: {keypoints.Count}");
                foreach (JArray point in keypoints)
                {
                    int x = point[0].Value<int>();
                    int y = point[1].Value<int>();
                    Debug.Log($"X: {x}, Y: {y}");
                    PlotKeypoint(x, y, texture);
                }
                texture.Apply();
                finalOutput.texture = texture;
            }
            else
            {
                Debug.LogWarning("No keypoints detected in the response.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to parse JSON response: " + ex.Message);
        }
    }

    private void PlotKeypoint(int x, int y, Texture2D texture)
    {
        if (texture == null) return;

        int invertedY = texture.height - y; // Invert Y due to webcam orientation

        for (int i = -keypointSize; i <= keypointSize; i++)
        {
            for (int j = -keypointSize; j <= keypointSize; j++)
            {
                int newX = Mathf.Clamp(x + i, 0, texture.width - 1);
                int newY = Mathf.Clamp(invertedY + j, 0, texture.height - 1);

                if (i * i + j * j <= keypointSize * keypointSize)
                {
                    texture.SetPixel(newX, newY, keypointColor);
                }
            }
        }
    }
}
