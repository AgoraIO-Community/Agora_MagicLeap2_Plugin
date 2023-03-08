using System.Collections;
using System.Collections.Generic;
using Agora.Rtc;
using UnityEngine;

namespace agora_sample
{
    public class VirtualCameraDemo : IVideoCaptureManager
    {
        [SerializeField]
        private Camera VirtualCam;
        [SerializeField]
        private RenderTexture VirtualCamRT;

        [Header("Video Encoder Config")]
        [SerializeField]
        private VideoDimensions dimensions = new VideoDimensions
        {
            width = 1280,
            height = 720
        };
        //[SerializeField]
        //private int bitrate = 1130;
        //private FRAME_RATE frameRate = FRAME_RATE.FRAME_RATE_FPS_30;
        //[SerializeField]
        //private VIDEO_MIRROR_MODE_TYPE mirrorMode = VIDEO_MIRROR_MODE_TYPE.VIDEO_MIRROR_MODE_DISABLED;
        // use bitrate: 2260 for broadcast mode

        // Pixel format
        public static TextureFormat ConvertFormat = TextureFormat.RGBA32;
        public static VIDEO_PIXEL_FORMAT PixelFormat = VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA;

        // perspective camera buffer
        private Texture2D BufferTexture = null;
        private static int ShareCameraMode = 1;  // 0 = unsafe buffer pointer, 1 = renderer imag

        private IRtcEngine _rtc;
        private object _lock;
        public override void Init(IRtcEngine engine, object rtclock)
        {
            _rtc = engine;
            _lock = rtclock;
        }

        public override void ConnectCamera()
        {
            EnableVirtualCameraSharing();
        }

        public override void DisconnectCamera()
        {
            DisableSharing();
        }

        #region --- Virtual Camera video frame sharing ---


        void EnableVirtualCameraSharing()
        {
            RenderTexture renderTexture = VirtualCamRT;
            if (renderTexture != null)
            {
                BufferTexture = new Texture2D(renderTexture.width, renderTexture.height, ConvertFormat, false);
                StartCoroutine(CoShareRenderData()); // use co-routine to push frames into the Agora stream
            }
        }

        void DisableSharing()
        {
            BufferTexture = null;
        }

        IEnumerator CoShareRenderData()
        {
            while (ShareCameraMode == 1)
            {
                yield return new WaitForEndOfFrame();
                ShareRenderTexture();
            }
            yield return null;
        }

        private void ShareRenderTexture()
        {
            if (BufferTexture == null) // offlined
            {
                return;
            }
            Camera targetCamera = VirtualCam; // AR Camera
            RenderTexture.active = targetCamera.targetTexture; // the targetTexture holds render texture
            Rect rect = new Rect(0, 0, targetCamera.targetTexture.width, targetCamera.targetTexture.height);
            BufferTexture.ReadPixels(rect, 0, 0);
            BufferTexture.Apply();
            byte[] bytes = BufferTexture.GetRawTextureData();

            StartCoroutine(PushFrame(bytes, (int)rect.width, (int)rect.height,
             () =>
             {
                 bytes = null;
             }));

            RenderTexture.active = null;
        }

        long timestamp = 0;
        /// <summary>
        /// Push frame to the remote client.  This is the same code that does ScreenSharing.
        /// </summary>
        /// <param name="bytes">raw video image data</param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="onFinish">callback upon finish of the function</param>
        /// <returns></returns>
        IEnumerator PushFrame(byte[] bytes, int width, int height, System.Action onFinish)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("Zero bytes found!!!!");
                yield break;
            }

            //if the engine is present
            if (_rtc != null)
            {
                //Create a new external video frame
                ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
                //Set the buffer type of the video frame
                externalVideoFrame.type = VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
                // Set the video pixel format
                externalVideoFrame.format = PixelFormat; // VIDEO_PIXEL_RGBA
                                                         //apply raw data you are pulling from the rectangle you created earlier to the video frame
                externalVideoFrame.buffer = bytes;

                //Set the width of the video frame (in pixels)
                externalVideoFrame.stride = width;
                //Set the height of the video frame
                externalVideoFrame.height = height;

                //Remove pixels from the sides of the frame
                //externalVideoFrame.cropLeft = 10;
                //externalVideoFrame.cropTop = 10;
                //externalVideoFrame.cropRight = 10;
                //externalVideoFrame.cropBottom = 10;
                //Rotate the video frame (0, 90, 180, or 270)
                externalVideoFrame.rotation = 180;
                // increment i with the video timestamp
                //externalVideoFrame.timestamp = System.DateTime.Now.Ticks;
                externalVideoFrame.timestamp = 0;
                //Push the external video frame with the frame we just created
                int a = 0;
                lock (_lock)
                {
                    _rtc.PushVideoFrame(externalVideoFrame);
                }
                if (timestamp % 100 == 0) Debug.Log(" pushVideoFrame(" + timestamp + ") size:" + bytes.Length + " => " + a);
            }

            yield return null;
            onFinish();
        }
        #endregion
    }
}
