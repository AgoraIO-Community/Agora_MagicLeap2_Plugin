using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;
using agora_utilities;

namespace agora_sample
{
    /// <summary>
    ///    The AgoraController serves as the simple plugin controller for MagicLeap2.
    ///  It sets up the application with the essential Audio Video control, API methods and callbacks for
    ///  Agora Live Streaming purpose.   
    /// </summary>
    public class AgoraController : MonoBehaviour
    {
        [Header("Agora SDK Parameters")]
        [SerializeField]
        private string APP_ID = "";

        [SerializeField]
        [Tooltip("Use TokenClient to connect to a predefined token server. Unmark it if your AppID doesnot use token.")]
        bool UseToken = false;

        private string TOKEN = "";

        [SerializeField]
        private string CHANNEL_NAME = "YOUR_CHANNEL_NAME";

        [Header("UI Manager")]
        [SerializeField] GameObject SpawnPoint;
        [SerializeField] Text logText;
        [SerializeField] Transform ReferenceTransform;
        [SerializeField] ToggleStateButton ConnectButton;
        [SerializeField] ToggleStateButton MuteLocalButton;
        [SerializeField] ToggleStateButton MuteRemoteButton;

        // Video components
        IVideoRenderManager VideoRenderMgr;
        [SerializeField]
        CustomVideoCapturer CustomVideoCapture;

        [Header("Audio Control")]
        [SerializeField]
        CustomAudioSinkPlayer CustomAudioSink;
        [SerializeField]
        CustomAudioCapturer CustomAudioCapture;

        private agora_utilities.Logger _logger;
        private IRtcEngine _rtcEngine = null;
        private uint _clientUID = 0;  // used for join channel, default is 0

        private bool appReady = false;

        // Use this for initialization
        void Awake()
        {
            appReady = CheckAppId();
            if (appReady)
            {
                InitUI();
                VideoRenderMgr = new VideoRenderManager(SpawnPoint.transform, ReferenceTransform);
            }
        }

        private void Start()
        {
            // Assume automatically joining the agora channel
            if (appReady)
            {
                InitEngine(JoinChannel);
            }
        }

        // Simple check for APP ID input in case it is forgotten
        bool CheckAppId()
        {
            if (APP_ID.Length < 10)
            {
                Debug.LogError($"----- AppID must be provided for {name}! -----");
                return false;
            }
            _logger = new agora_utilities.Logger(logText);
            return true;
        }

        // Initialize Agora Game Engine
        void InitEngine(System.Action callback)
        {
            _rtcEngine = IRtcEngine.GetEngine(APP_ID);
            _rtcEngine.SetLogFile("log.txt");
            _rtcEngine.SetExternalAudioSource(true, CustomAudioCapturer.SAMPLE_RATE, CustomAudioCapturer.CHANNEL);
            _rtcEngine.SetChannelProfile(CHANNEL_PROFILE.CHANNEL_PROFILE_LIVE_BROADCASTING);
            _rtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
            _rtcEngine.SetAudioProfile(AUDIO_PROFILE_TYPE.AUDIO_PROFILE_DEFAULT, AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_GAME_STREAMING);
            _rtcEngine.EnableVideo();
            _rtcEngine.EnableVideoObserver();
            _rtcEngine.SetExternalVideoSource(true);

            _rtcEngine.SetDefaultAudioRouteToSpeakerphone(true);

            // use external audio sink
            if (CustomAudioSink != null)
            {
                Debug.Log("[Agora] Using Custom Audio Sink");
                _rtcEngine.SetExternalAudioSink(true, CustomAudioSink.SAMPLE_RATE, CustomAudioSink.CHANNEL);
            }


            // Register event handlers
            _rtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;
            _rtcEngine.OnLeaveChannel += OnLeaveChannelHandler;
            _rtcEngine.OnWarning += OnSDKWarningHandler;
            _rtcEngine.OnError += OnSDKErrorHandler;
            _rtcEngine.OnConnectionLost += OnConnectionLostHandler;
            _rtcEngine.OnUserJoined += OnUserJoinedHandler;
            _rtcEngine.OnUserOffline += OnUserOfflineHandler;
            _rtcEngine.OnVideoSizeChanged += OnVideoSizeChanged;


            // If AppID is certifcate enabled, use token.
            if (UseToken)
            {
                TokenClient.Instance?.GetTokens(CHANNEL_NAME, _clientUID, (token, _) =>
                {
                    TOKEN = token;
                    Debug.Log("Gotten token:" + token);
                    callback();
                });
            }
            else
            {
                callback();
            }
        }

        // Demo UI setup, using custom ToggleStateButton class
        void InitUI()
        {
            ConnectButton.Setup(false, "Connect Camera", "Disconnect Camera",
                callOnAction: () =>
                {
                    CustomVideoCapture.ConnectCamera();
                },
                callOffAction: () =>
                {
                    CustomVideoCapture.DisconnectCamera();
                });

            MuteLocalButton.Setup(false, "Mute Local", "UnMute Local",
                callOnAction: () =>
                {
                    _rtcEngine.MuteLocalAudioStream(true);
                },
                callOffAction: () => { _rtcEngine.MuteLocalAudioStream(false); });

            MuteRemoteButton.Setup(false, "Mute Remote", "UnMute Remote",
                callOnAction: () => { _rtcEngine.MuteAllRemoteAudioStreams(true); },
                callOffAction: () => { _rtcEngine.MuteAllRemoteAudioStreams(false); });
        }

        void JoinChannel()
        {
            _rtcEngine.JoinChannelByKey(TOKEN, CHANNEL_NAME, "", _clientUID);
        }

        #region -- Agora Event Callbacks --
        void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
        {
            _logger.UpdateLog(string.Format("sdk version: {0}", IRtcEngine.GetSdkVersion()));
            _logger.UpdateLog(string.Format("onJoinChannelSuccess channelName: {0}, uid: {1}, elapsed: {2}", channelName,
                uid, elapsed));

            // Start pushing audio data
            CustomAudioCapture.StartPushAudioFrame();
        }

        void OnLeaveChannelHandler(RtcStats stats)
        {
            _logger.UpdateLog("OnLeaveChannelSuccess");
            CustomAudioCapture.StopAudioPush();
        }

        void OnUserJoinedHandler(uint uid, int elapsed)
        {
            _logger.UpdateLog(string.Format("OnUserJoined uid: {0} elapsed: {1}", uid, elapsed));
            VideoRenderMgr.MakeVideoView(uid);
        }

        void OnUserOfflineHandler(uint uid, USER_OFFLINE_REASON reason)
        {
            _logger.UpdateLog(string.Format("OnUserOffLine uid: {0}, reason: {1}", uid, (int)reason));
            VideoRenderMgr.DestroyVideoView(uid);
        }

        void OnSDKWarningHandler(int warn, string msg)
        {
            _logger.UpdateLog(string.Format("OnSDKWarning warn: {0}, msg: {1}", warn, IRtcEngine.GetErrorDescription(warn)));
        }

        void OnSDKErrorHandler(int error, string msg)
        {
            _logger.UpdateLog(string.Format("OnSDKError error: {0}, msg: {1}", error, IRtcEngine.GetErrorDescription(error)));
        }

        void OnConnectionLostHandler()
        {
            _logger.UpdateLog(string.Format("OnConnectionLost "));
        }

        void OnVideoSizeChanged(uint uid, int width, int height, int rotation)
        {
            VideoRenderMgr.UpdateVideoView(uid, width, height, rotation);
        }

        #endregion

        private void OnDestroy()
        {
            Debug.Log("OnDestroy: Agora Clean up");
            if (_rtcEngine != null)
            {
                _rtcEngine.LeaveChannel();
                _rtcEngine.DisableVideoObserver();

                // Important: clean up the engine as the last step
                IRtcEngine.Destroy();
                _rtcEngine = null;
            }
        }

    }
}
