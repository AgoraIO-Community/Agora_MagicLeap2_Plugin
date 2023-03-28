using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ML2_ENABLE
using UnityEngine.XR.MagicLeap;
using static UnityEngine.XR.MagicLeap.MLCamera;
#endif

using Agora.Rtc;

namespace Agora.Rtc.Extended
{
    public class CustomVideoCapturer : IVideoCaptureManager
    {
        private IRtcEngine _rtcEngine = null;
        private object _rtclock = null;
#if ML2_ENABLE
        #region -- MagicLeap --

        private bool IsCameraConnected => captureCamera != null && captureCamera.ConnectionEstablished;

        private List<MLCamera.StreamCapability> streamCapabilities;

        private MLCamera captureCamera;
        private bool cameraDeviceAvailable;
        private bool isCapturingVideo = false;

        // Reference BitRate/FrameRate/Resolution table here:
        // https://docs.agora.io/en/Interactive%20Broadcast/API%20Reference/java/classio_1_1agora_1_1rtc_1_1video_1_1_video_encoder_configuration.html#a4b090cd0e9f6d98bcf89cb1c4c2066e8
        [SerializeField, Tooltip("Kpbs, see Agora API doc for details")]
        int BitRate = 1000;

        [SerializeField]
        MLCamera.CaptureFrameRate MLFrameRate = MLCamera.CaptureFrameRate._30FPS;
        [SerializeField]
        MLCamera.MRQuality MLQuality = MLCamera.MRQuality._648x720;
        [SerializeField]
        MLCamera.ConnectFlag MLConnectFlag = MLCamera.ConnectFlag.MR;

        private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();


        private void Awake()
        {
            permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
            permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
            permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;
        }

        private IEnumerator Start()
        {
            MLPermissions.RequestPermission(MLPermission.Camera, permissionCallbacks);
            MLPermissions.RequestPermission(MLPermission.RecordAudio, permissionCallbacks);

            yield return null;
            TryEnableMLCamera();
        }

        /// <summary>
        /// Stop the camera, unregister callbacks.
        /// </summary>
        void OnDisable()
        {
            DisconnectCamera();
        }

        public override void Init(Agora.Rtc.IRtcEngine engine, object rtclock)
        {
            _rtcEngine = engine;
            _rtclock = rtclock;
            // Agora does not have direct access to ML2 camera, so enable external source for input 
            var ret = _rtcEngine.SetExternalVideoSource(true, false, EXTERNAL_VIDEO_SOURCE_TYPE.VIDEO_FRAME, new SenderOptions());
            Debug.Log("SetExternalVideoSource returns:" + ret);
        }

        private void OnPermissionDenied(string permission)
        {
            if (permission == MLPermission.Camera)
            {
#if UNITY_ANDROID
                MLPluginLog.Error($"{permission} denied, example won't function.");
#endif
            }
            else if (permission == MLPermission.RecordAudio)
            {
#if UNITY_ANDROID
                MLPluginLog.Error($"{permission} denied, audio wont be recorded in the file.");
#endif
            }

        }

        private void OnPermissionGranted(string permission)
        {
#if UNITY_ANDROID
            MLPluginLog.Debug($"Granted {permission}.");
            TryEnableMLCamera();
#endif
        }


        private void TryEnableMLCamera()
        {
            if (!MLPermissions.CheckPermission(MLPermission.Camera).IsOk)
            {
                Debug.LogError("Permission not ok");
                MLPluginLog.Warning("ML camera permission is not ok");
                return;
            }

            StartCoroutine(EnableMLCamera());
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
            MLPluginLog.Warning("camera device available. connecting camera...");

            yield return new WaitForSeconds(2f);
        }

        /// <summary>
        /// Connects to the MLCamera.
        /// </summary>
        public override void ConnectCamera()
        {
            MLCamera.ConnectContext context = MLCamera.ConnectContext.Create();
            context.Flags = MLConnectFlag;
            context.EnableVideoStabilization = true;

            if (context.Flags != MLCamera.ConnectFlag.CamOnly)
            {
                context.MixedRealityConnectInfo = MLCamera.MRConnectInfo.Create();
                context.MixedRealityConnectInfo.MRQuality = MLQuality;
                context.MixedRealityConnectInfo.MRBlendType = MLCamera.MRBlendType.Additive;
                context.MixedRealityConnectInfo.FrameRate = MLFrameRate;
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
        public override void DisconnectCamera()
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
            captureConfig.CaptureFrameRate = MLFrameRate;
            captureConfig.StreamConfigs = new MLCamera.CaptureStreamConfig[1];
            captureConfig.StreamConfigs[0] =
                MLCamera.CaptureStreamConfig.Create(GetStreamCapability(), MLCamera.OutputFormat.RGBA_8888);

            MLResult result = captureCamera.PrepareCapture(captureConfig, out MLCamera.Metadata _);
            SetAgoraEncoderConfiguration();

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

        void SetAgoraEncoderConfiguration()
        {
            int width = 320;
            int height = 640;

            switch (MLQuality)
            {
                case MLCamera.MRQuality._648x720:
                    width = 648;
                    height = 720;
                    break;
                case MLCamera.MRQuality._972x1080:
                    width = 972;
                    height = 1080;
                    break;
                case MLCamera.MRQuality._960x720:
                    width = 960;
                    height = 720;
                    break;
                case MLCamera.MRQuality._1440x1080:
                    width = 1440;
                    height = 1080;
                    break;
                case MLCamera.MRQuality._1944x2160:
                    width = 1944;
                    height = 2160;
                    break;
                case MLCamera.MRQuality._2880x2160:
                    width = 2880;
                    height = 2160;
                    break;
            }

            FRAME_RATE frame_rate = FRAME_RATE.FRAME_RATE_FPS_30;
            switch (MLFrameRate)
            {
                case MLCamera.CaptureFrameRate._15FPS:
                    frame_rate = FRAME_RATE.FRAME_RATE_FPS_15;
                    break;
                case MLCamera.CaptureFrameRate._60FPS:
                    frame_rate = FRAME_RATE.FRAME_RATE_FPS_60;
                    break;
            }
            _rtcEngine.SetVideoEncoderConfiguration(new VideoEncoderConfiguration
            {
                frameRate = (int)frame_rate,
                bitrate = BitRate,
                dimensions = new VideoDimensions { width = width, height = height }
            });
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
                                                     MLCamera.ResultExtras resultExtras, MLCamera.Metadata metadata)
        {
            // cameraCaptureVisualizer.OnCaptureDataReceived(resultExtras, capturedFrame);
            // Debug.Log("RawVideoFrameAvailable:" + capturedFrame.ToString());
            var plane = capturedFrame.Planes[0];
            byte[] data = plane.Data.ToArray(); // a copy
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
                externalVideoFrame.type = VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
                externalVideoFrame.format = VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA;
                externalVideoFrame.buffer = bytes;
                //Set the width of the video frame (in pixels)
                externalVideoFrame.stride = width;
                //Set the height of the video frame
                externalVideoFrame.height = height;
                //Remove pixels from the sides of the frame
                //Rotate the video frame (0, 90, 180, or 270)
                //externalVideoFrame.rotation = 180;
                externalVideoFrame.timestamp = 0;
                //Push the external video frame with the frame we just created
                lock (_rtclock)
                {
                    _rtcEngine.PushVideoFrame(externalVideoFrame);
                }
                if (++timestamp % 100 == 0)
                {
                    Debug.Log("Pushed video frame = " + timestamp);
                }

            }
        }
        #endregion
#else
        public override void Init(IRtcEngine engine, object rtclock)
        {
            throw new System.NotImplementedException();
        }

        public override void ConnectCamera()
        {
            throw new System.NotImplementedException();
        }

        public override void DisconnectCamera()
        {
            throw new System.NotImplementedException();
        }
#endif
    }
}
