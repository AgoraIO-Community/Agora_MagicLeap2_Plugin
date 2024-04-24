using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;
using Agora.Util;
using Logger = Agora_RTC_Plugin.API_Example.Logger;

namespace Agora.Rtc.Extended
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
        public bool UseTokenClient = false;

        [SerializeField]
        private string TOKEN = "";

        [SerializeField]
        private string CHANNEL_NAME = "YOUR_CHANNEL_NAME";

        [SerializeField]
        CLIENT_ROLE_TYPE CLIENT_ROLE = CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER;
        [SerializeField]
        LOG_LEVEL LogLevel = LOG_LEVEL.LOG_LEVEL_INFO;

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
        IVideoCaptureManager CustomVideoCapture;

        [Header("Audio Control")]
        [SerializeField]
        IAudioRenderManager CustomAudioSink;
        [SerializeField]
        IAudioCaptureManager CustomAudioCapture;

        internal uint AUDIO_TRACK_ID = 0;

        internal Logger _logger;
        private IRtcEngine _rtcEngine = null;
        private uint _clientUID = 0;  // used for join channel, default is 0

        private bool appReady = false;

        // Use this lock for protecting single access to RTC API calls
        public static readonly object RtcLock = new object();

        // Use this for initialization
        void Awake()
        {
            appReady = CheckAppId();
            if (appReady)
            {
                InitUI();
                VideoRenderMgr = new VideoRenderManager(CHANNEL_NAME, SpawnPoint.transform, ReferenceTransform);
            }
        }

        private void Start()
        {
            // Assume automatically joining the agora channel
            if (appReady)
            {
                InitEngine();
                CreateCustomAudioSource();
                PrepareJoinChannel(callback: JoinChannel);
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
            _logger = new Logger(logText);
            return true;
        }

        // Initialize Agora Game Engine
        void InitEngine()
        {

            _rtcEngine = RtcEngine.CreateAgoraRtcEngine();
            UserEventHandler handler = new UserEventHandler(this);
            RtcEngineContext context = new RtcEngineContext()
            {
                appId = APP_ID,
                channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING,
                audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_GAME_STREAMING,
                areaCode = AREA_CODE.AREA_CODE_GLOB
            };
            var rc = _rtcEngine.Initialize(context);
            Debug.Assert(rc == 0, "rtcEngine init failed");
            rc = _rtcEngine.InitEventHandler(handler);
            Debug.Assert(rc == 0, "rtcEngine init handler failed");

            _rtcEngine.EnableAudio();
            _rtcEngine.EnableVideo();

            _rtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);

            _rtcEngine.SetLogLevel(LogLevel);
            _rtcEngine.SetLogFile(Application.persistentDataPath + "/log.txt");

            CustomVideoCapture?.Init(_rtcEngine, RtcLock);
            CustomAudioSink?.Init(_rtcEngine, RtcLock);
            CustomAudioCapture?.Init(_rtcEngine, RtcLock);
        }

        void PrepareJoinChannel(System.Action callback)
        {
            // If AppID is certifcate enabled, use token.
            if (UseTokenClient)
            {
                TokenClient.Instance.SetClient(
                    CLIENT_ROLE == CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER ? ClientType.publisher : ClientType.subscriber);

                TokenClient.Instance.GetRtcToken(CHANNEL_NAME, _clientUID, (token) =>
                {
                    TOKEN = token;
                    Debug.Log("Agora rtc token:" + token);
                    callback();
                });
            }
            else
            {
                callback();
            }
        }

        void CreateCustomAudioSource()
        {
            lock (RtcLock)
            {
                AudioTrackConfig audioTrackConfig = new AudioTrackConfig(enableLocalPlayback: false);
                AUDIO_TRACK_ID = _rtcEngine.CreateCustomAudioTrack(AUDIO_TRACK_TYPE.AUDIO_TRACK_DIRECT, audioTrackConfig);
                _logger.UpdateLog("AUDIO_TRACK_ID:" + AUDIO_TRACK_ID);
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
            var option = new ChannelMediaOptions();
            option.autoSubscribeVideo.SetValue(true);
            option.autoSubscribeAudio.SetValue(true);
            option.publishMicrophoneTrack.SetValue(false);
            option.publishCameraTrack.SetValue(false);
            option.publishCustomAudioTrack.SetValue(true);
            option.publishCustomAudioTrackId.SetValue((int)AUDIO_TRACK_ID);
            option.publishCustomVideoTrack.SetValue(true);
            option.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
            option.channelProfile.SetValue(CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING);

            _rtcEngine.JoinChannel(TOKEN, CHANNEL_NAME, _clientUID, option);
        }


        void OnVideoSizeChanged(uint uid, int width, int height, int rotation)
        {
            VideoRenderMgr.UpdateVideoView(uid, width, height, rotation);
        }


        private void OnDestroy()
        {
            Debug.Log("OnDestroy: Agora Clean up");
            if (_rtcEngine != null)
            {
                _rtcEngine.LeaveChannel();

                // Important: clean up the engine as the last step
                _rtcEngine.Dispose();
                _rtcEngine = null;
            }
        }

        internal class UserEventHandler : IRtcEngineEventHandler
        {
            private readonly AgoraController _app;

            internal UserEventHandler(AgoraController agoraController)
            {
                _app = agoraController;
            }

            #region -- Agora Event Callbacks --
            public override void OnError(int err, string msg)
            {
                _app._logger.UpdateLog(string.Format("OnError err: {0}, msg: {1}", err, _app._rtcEngine.GetErrorDescription(err)));
            }

            public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
            {
                int build = 0;
                _app._logger.UpdateLog(string.Format("sdk version: ${0}",
                    _app._rtcEngine.GetVersion(ref build)));
                _app._logger.UpdateLog(string.Format("OnJoinChannelSuccess channelName: {0}, uid: {1}, elapsed: {2}",
                        connection.channelId, connection.localUid, elapsed));
                _app.CustomAudioCapture?.StartAudioPush();
            }

            public override void OnRejoinChannelSuccess(RtcConnection connection, int elapsed)
            {
                _app._logger.UpdateLog("OnRejoinChannelSuccess");
            }

            public override void OnLeaveChannel(RtcConnection connection, RtcStats stats)
            {
                _app._logger.UpdateLog("OnLeaveChannel");
                _app.CustomAudioCapture?.StopAudioPush();
            }

            public override void OnClientRoleChanged(RtcConnection connection, CLIENT_ROLE_TYPE oldRole, CLIENT_ROLE_TYPE newRole, ClientRoleOptions newRoleOptions)
            {
                _app._logger.UpdateLog("OnClientRoleChanged");
                TokenClient.Instance.OnClientRoleChangedHandler(oldRole, newRole);
            }

            public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
            {
                _app._logger.UpdateLog(string.Format("OnUserJoined uid: {0} elapsed: {1}", uid, elapsed));
                _app.VideoRenderMgr.MakeVideoView(uid);
            }

            public override void OnUserOffline(RtcConnection connection, uint uid, USER_OFFLINE_REASON_TYPE reason)
            {
                _app._logger.UpdateLog(string.Format("OnUserOffLine uid: {0}, reason: {1}", uid, (int)reason));
                _app.VideoRenderMgr.DestroyVideoView(uid);
            }

            public override void OnVideoSizeChanged(RtcConnection connection, VIDEO_SOURCE_TYPE sourceType, uint uid, int width, int height, int rotation)
            {
                _app.VideoRenderMgr.UpdateVideoView(uid, width, height, rotation);
            }

            public override void OnTokenPrivilegeWillExpire(RtcConnection connection, string token)
            {
                if (_app.UseTokenClient)
                {
                    base.OnTokenPrivilegeWillExpire(connection, token);
                    TokenClient.Instance.OnTokenPrivilegeWillExpireHandler(token);
                }
                else
                {
                    // if you are using your own logic without the TokenClient, please implement here
                    _app._logger.UpdateLog(string.Format("OnTokenPrivilegeWillExpire, connection:" + connection.channelId));
                }
            }

            public override void OnConnectionLost(RtcConnection connection)
            {
                _app._logger.UpdateLog(string.Format("OnConnectionLost "));
            }
            #endregion
        }
    }
}
