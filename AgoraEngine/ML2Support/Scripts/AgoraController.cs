﻿using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;
using Agora.Util;

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
        public bool UseToken = false;

        private string TOKEN = "";

        [SerializeField]
        private string CHANNEL_NAME = "YOUR_CHANNEL_NAME";

        [SerializeField]
        CLIENT_ROLE_TYPE CLIENT_ROLE = CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER;

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
        CustomAudioSinkPlayer CustomAudioSink;
        [SerializeField]
        CustomAudioCapturer CustomAudioCapture;

        internal agora_utilities.Logger _logger;
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
                VideoRenderMgr = new VideoRenderManager(CHANNEL_NAME, SpawnPoint.transform, ReferenceTransform);
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

            _rtcEngine = RtcEngine.CreateAgoraRtcEngine();
            UserEventHandler handler = new UserEventHandler(this);
            RtcEngineContext context = new RtcEngineContext(
                appId: APP_ID,
                context: 0,
                channelProfile: CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING,
                audioScenario: AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT,
                areaCode: AREA_CODE.AREA_CODE_GLOB
                );
            var rc = _rtcEngine.Initialize(context);
            Debug.Assert(rc == 0, "rtcEngine init failed");
            rc = _rtcEngine.InitEventHandler(handler);
            Debug.Assert(rc == 0, "rtcEngine init handler failed");

            _rtcEngine.EnableAudio();
            _rtcEngine.SetExternalAudioSource(true, CustomAudioCapturer.SAMPLE_RATE, CustomAudioCapturer.CHANNEL, 1);

            _rtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
            _rtcEngine.SetAudioProfile(AUDIO_PROFILE_TYPE.AUDIO_PROFILE_DEFAULT, AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_GAME_STREAMING);

            _rtcEngine.EnableVideo();
            // Agora does not have direct access to ML2 camera, so enable external source for input 
            var ret = _rtcEngine.SetExternalVideoSource(true, false, EXTERNAL_VIDEO_SOURCE_TYPE.VIDEO_FRAME, new SenderOptions());
            this._logger.UpdateLog("SetExternalVideoSource returns:" + ret);

            // _rtcEngine.SetDefaultAudioRouteToSpeakerphone(true);

            // use external audio sink
            if (CustomAudioSink != null)
            {
                Debug.Log("[Agora] Using Custom Audio Sink");
                //_rtcEngine.SetExternalAudioSink(true, CustomAudioSink.SAMPLE_RATE, CustomAudioSink.CHANNEL);
                CustomAudioSink.InitEngineSink(_rtcEngine);
            }


            // If AppID is certifcate enabled, use token.
            if (UseToken)
            {
                TokenClient.Instance.SetClient(
          CLIENT_ROLE == CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER ? ClientType.publisher : ClientType.subscriber);

                TokenClient.Instance.GetRtcToken(CHANNEL_NAME, _clientUID, (token) =>
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

            //var mlInputs = new MagicLeapInputs();
            //mlInputs.Enable();
            //var controllerActions = new MagicLeapInputs.ControllerActions(mlInputs);

            //controllerActions.Bumper.performed += HandleOnBumperDown;
            // controllerActions.Trigger.performed += HandleOnTriggerDown;
        }

        void JoinChannel()
        {
            //     _rtcEngine.JoinChannel(TOKEN, CHANNEL_NAME, "", _clientUID);

            var option = new ChannelMediaOptions();
            option.autoSubscribeVideo.SetValue(true);
            option.autoSubscribeAudio.SetValue(true);
            option.publishMicrophoneTrack.SetValue(false);
            option.publishCameraTrack.SetValue(false);
            option.publishCustomAudioTrack.SetValue(true);
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
                _app.CustomAudioCapture?.StartPushAudioFrame();
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
                if (_app.UseToken)
                {
                    base.OnTokenPrivilegeWillExpire(connection, token);
                    TokenClient.Instance.OnTokenPrivilegeWillExpireHandler(token);
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
