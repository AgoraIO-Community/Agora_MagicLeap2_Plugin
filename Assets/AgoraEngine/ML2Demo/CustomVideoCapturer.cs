using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

using agora_gaming_rtc;

public class CustomVideoCapturer : MonoBehaviour
{
    #region -- MagicLeap --

    private bool IsCameraConnected => captureCamera != null && captureCamera.ConnectionEstablished;

    private List<MLCamera.StreamCapability> streamCapabilities;

    private MLCamera captureCamera;
    private bool cameraDeviceAvailable;
    private bool isCapturingVideo = false;

    [SerializeField]
    MLCamera.CaptureFrameRate FrameRate = MLCamera.CaptureFrameRate._30FPS;
    [SerializeField]
    MLCamera.MRQuality MRQuality = MLCamera.MRQuality._648x720;
    [SerializeField]
    MLCamera.ConnectFlag MRConnectFlag = MLCamera.ConnectFlag.MR;

    IRtcEngine _rtcEngine = null;

    private async void Start()
    {
        var requestResult = await RequestPermission(MLPermission.Camera);
        if (!requestResult)
        {
            Debug.LogError($"Camera capture will not be available.");
            return;
        }
        StartCoroutine(EnableMLCamera());
        while (IRtcEngine.QueryEngine() == null)
        {
            await Task.Delay(50);
        }
        _rtcEngine = IRtcEngine.QueryEngine();
    }

    /// <summary>
    /// Stop the camera, unregister callbacks.
    /// </summary>
    void OnDisable()
    {
        DisconnectCamera();
    }

    private async Task<bool> RequestPermission(string permission)
    {
        if (MLPermissions.CheckPermission(permission) == MLResult.Code.Ok)
            return true;

        bool answer = false;
        bool requestResult = false;

        MLPermissions.Callbacks requestCallbacks = new MLPermissions.Callbacks();
        requestCallbacks.OnPermissionGranted += grantedPermission =>
        {
            answer = true;
            requestResult = true;
        };

        requestCallbacks.OnPermissionDenied += deniedPermission =>
        {
            answer = true;

            Debug.LogError($"Request for {deniedPermission} permission was denied.");
        };

        requestCallbacks.OnPermissionDeniedAndDontAskAgain += deniedPermission =>
        {
            answer = true;

            Debug.LogError($"Request for {deniedPermission} permission was denied.");
        };

        var result = MLPermissions.RequestPermission(permission, requestCallbacks);
        if (!MLResult.DidNativeCallSucceed(result.Result, nameof(MLPermissions.RequestPermission)))
        {
            Debug.LogError($"Request for {permission} permission was denied.");
            return false;
        }

        var waitTask = Task.Run(async () =>
        {
            while (!answer)
                await Task.Delay(25);
        });

        if (waitTask != await Task.WhenAny(waitTask))
            throw new TimeoutException();

        return requestResult;
    }

    /// <summary>
    /// Connects the MLCamera component and instantiates a new instance
    /// if it was never created.
    /// </summary>
    private IEnumerator EnableMLCamera()
    {
        while (!cameraDeviceAvailable)
        {
            MLResult result =
                MLCamera.GetDeviceAvailabilityStatus(MLCamera.Identifier.Main, out cameraDeviceAvailable);
            if (!(result.IsOk && cameraDeviceAvailable))
            {
                // Wait until camera device is available
                yield return new WaitForSeconds(1.0f);
            }
        }

        Debug.Log("Camera device available");
        ConnectCamera();
    }

    /// <summary>
    /// Connects to the MLCamera.
    /// </summary>
    private void ConnectCamera()
    {
        MLCamera.ConnectContext context = MLCamera.ConnectContext.Create();
        context.Flags = MRConnectFlag;
        context.EnableVideoStabilization = true;

        if (context.Flags != MLCamera.ConnectFlag.CamOnly)
        {
            context.MixedRealityConnectInfo = MLCamera.MRConnectInfo.Create();
            context.MixedRealityConnectInfo.MRQuality = MRQuality;
            context.MixedRealityConnectInfo.MRBlendType = MLCamera.MRBlendType.Additive;
            context.MixedRealityConnectInfo.FrameRate = FrameRate;
        }

        captureCamera = MLCamera.CreateAndConnect(context);

        if (captureCamera != null)
        {
            Debug.Log("Camera device connected");
            if (GetImageStreamCapabilities())
            {
                Debug.Log("Camera device received stream caps");
                captureCamera.OnRawVideoFrameAvailable += OnCaptureRawVideoFrameAvailable;
                StartVideoCapture();
            }
        }
    }

    /// <summary>
    /// Disconnects the camera.
    /// </summary>
    private void DisconnectCamera()
    {
        if (captureCamera == null || !IsCameraConnected)
            return;

        streamCapabilities = null;

        captureCamera.OnRawVideoFrameAvailable -= OnCaptureRawVideoFrameAvailable;
        captureCamera.Disconnect();
    }

    private IEnumerator StopVideo()
    {
        float startTimestamp = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTimestamp < 10)
        {
            yield return null;
        }

        StopVideoCapture();
    }


    /// <summary>
    /// Captures a preview of the device's camera and displays it in front of the user.
    /// If Record to File is selected then it will not show the preview.
    /// </summary>
    private void StartVideoCapture()
    {
        MLCamera.CaptureConfig captureConfig = new MLCamera.CaptureConfig();
        captureConfig.CaptureFrameRate = FrameRate;
        captureConfig.StreamConfigs = new MLCamera.CaptureStreamConfig[1];
        captureConfig.StreamConfigs[0] =
            MLCamera.CaptureStreamConfig.Create(GetStreamCapability(), MLCamera.OutputFormat.RGBA_8888);

        MLResult result = captureCamera.PrepareCapture(captureConfig, out MLCamera.Metadata _);

        if (MLResult.DidNativeCallSucceed(result.Result, nameof(captureCamera.PrepareCapture)))
        {
            captureCamera.PreCaptureAEAWB();

            result = captureCamera.CaptureVideoStart();
            isCapturingVideo = MLResult.DidNativeCallSucceed(result.Result, nameof(captureCamera.CaptureVideoStart));
            if (isCapturingVideo)
            {
                // cameraCaptureVisualizer.DisplayCapture(captureConfig.StreamConfigs[0].OutputFormat, RecordToFile);
            }
        }

    }


    /// <summary>
    /// Stops the Video Capture.
    /// </summary>
    private void StopVideoCapture()
    {
        if (!isCapturingVideo)
            return;

        if (isCapturingVideo)
        {
            captureCamera.CaptureVideoStop();
        }

        // cameraCaptureVisualizer.HideRenderer();

        isCapturingVideo = false;
    }


    /// <summary>
    /// Gets the Image stream capabilities.
    /// </summary>
    /// <returns>True if MLCamera returned at least one stream capability.</returns>
    private bool GetImageStreamCapabilities()
    {
        var result =
            captureCamera.GetStreamCapabilities(out MLCamera.StreamCapabilitiesInfo[] streamCapabilitiesInfo);

        if (!result.IsOk)
        {
            Debug.Log("Could not get Stream capabilities Info.");
            return false;
        }

        streamCapabilities = new List<MLCamera.StreamCapability>();

        for (int i = 0; i < streamCapabilitiesInfo.Length; i++)
        {
            foreach (var streamCap in streamCapabilitiesInfo[i].StreamCapabilities)
            {
                streamCapabilities.Add(streamCap);
            }
        }

        return streamCapabilities.Count > 0;
    }


    /// <summary>
    /// Gets currently selected StreamCapability
    /// </summary>
    private MLCamera.StreamCapability GetStreamCapability()
    {
        foreach (var streamCapability in streamCapabilities.Where(s => s.CaptureType == MLCamera.CaptureType.Video))
        {
            return streamCapability;
        }

        Debug.LogWarning("Not finding Video capability, return first in the choice");
        return streamCapabilities[0];
    }

    /// <summary>
    /// Handles the event of a new image getting captured.
    /// </summary>
    /// <param name="capturedFrame">Captured Frame.</param>
    /// <param name="resultExtras">Result Extra.</param>
    private void OnCaptureRawVideoFrameAvailable(MLCamera.CameraOutput capturedFrame,
                                                 MLCamera.ResultExtras resultExtras)
    {
        // cameraCaptureVisualizer.OnCaptureDataReceived(resultExtras, capturedFrame);
        // Debug.Log("RawVideoFrameAvailable:" + capturedFrame.ToString());
        var plane = capturedFrame.Planes[0];
        byte[] data = plane.Data;
        ShareScreen(data, (int)(plane.Stride / plane.PixelStride), (int)plane.Height);
    }

    #endregion

    #region -- Video Pushing --
    long timestamp = 0;
    void ShareScreen(byte[] bytes, int width, int height)
    {
        // Check to see if there is an engine instance already created
        //if the engine is present
        if (_rtcEngine != null)
        {
            //Create a new external video frame
            ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
            //Set the buffer type of the video frame
            externalVideoFrame.type = ExternalVideoFrame.VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
            // Set the video pixel format
            externalVideoFrame.format = ExternalVideoFrame.VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA;
            externalVideoFrame.buffer = bytes;
            //Set the width of the video frame (in pixels)
            externalVideoFrame.stride = width;
            //Set the height of the video frame
            externalVideoFrame.height = height;
            //Remove pixels from the sides of the frame
            //Rotate the video frame (0, 90, 180, or 270)
            //externalVideoFrame.rotation = 180;
            externalVideoFrame.timestamp = timestamp++;
            //Push the external video frame with the frame we just created
            _rtcEngine.PushVideoFrame(externalVideoFrame);
            if (timestamp % 100 == 0)
            {
                Debug.Log("Pushed frame = " + timestamp);
            }

        }
    }
    #endregion
}
