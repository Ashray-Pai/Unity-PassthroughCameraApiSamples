using PassthroughCameraSamples.CameraToWorld;
using PassthroughCameraSamples;
using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using System;
using UnityEngine.UI;

public class CustomCamToCanvas : MonoBehaviour
{
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    [SerializeField] private GameObject m_centerEyeAnchor;
    [SerializeField] private CameraToWorldCameraCanvas m_cameraCanvas;

    [SerializeField] private float m_canvasDistance = 1f;
    [SerializeField] private RawImage m_image;

    private bool m_isDebugOn;
    private bool m_snapshotTaken;
    private OVRPose m_snapshotHeadPose;

    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;

    private IEnumerator Start()
    {
        if (m_webCamTextureManager == null)
        {
            enabled = false;
            yield break;
        }

        // Make sure the manager is disabled in scene and enable it only when the required permissions have been granted
        Assert.IsFalse(m_webCamTextureManager.enabled);
        while (PassthroughCameraPermissions.HasCameraPermission != true)
        {
            yield return null;
        }

        // Set the 'requestedResolution' and enable the manager
        m_webCamTextureManager.RequestedResolution = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye).Resolution;
        m_webCamTextureManager.enabled = true;

        ScaleCameraCanvas();
    }

    /// <summary>
    /// Calculate the dimensions of the canvas based on the distance from the camera origin and the camera resolution
    /// </summary>
    private void ScaleCameraCanvas()
    {
        var cameraCanvasRectTransform = m_cameraCanvas.GetComponentInChildren<RectTransform>(); ;
        var leftSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(0, CameraResolution.y / 2));
        var rightSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(CameraResolution.x, CameraResolution.y / 2));
        var horizontalFoVDegrees = Vector3.Angle(leftSidePointInCamera.direction, rightSidePointInCamera.direction);
        var horizontalFoVRadians = horizontalFoVDegrees / 180 * Math.PI;
        var newCanvasWidthInMeters = 2 * m_canvasDistance * Math.Tan(horizontalFoVRadians / 2);
        var localScale = (float)(newCanvasWidthInMeters / cameraCanvasRectTransform.sizeDelta.x);
        cameraCanvasRectTransform.localScale = new Vector3(localScale, localScale, localScale);
    }

    private void Update()
    {
        if (m_webCamTextureManager.WebCamTexture == null)
            return;

        UpdateCanvasPose();
    }

    private void UpdateCanvasPose()
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);

        // Position the canvas in front of the camera
        m_cameraCanvas.transform.position = cameraPose.position + cameraPose.rotation * Vector3.forward * m_canvasDistance;
        m_cameraCanvas.transform.rotation = cameraPose.rotation;
    }
}
