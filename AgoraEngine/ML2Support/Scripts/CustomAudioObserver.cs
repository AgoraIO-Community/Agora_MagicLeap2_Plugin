using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Agora.Rtc;
using RingBuffer;
using Agora_RTC_Plugin.API_Example.Examples.Advanced.ProcessAudioRawData;
using Agora.Util;

namespace Agora.Rtc.Extended
{
    /// <summary>
    /// The Custom AudioSink Player class receives audio frames from the
    /// Agora channel and applies the buffer to an AudioSource for playback.
    /// </summary>
    public class CustomAudioObserver : IAudioRenderManager
    {
        internal IRtcEngine RtcEngine;

        public readonly int CHANNEL = 1;
        public readonly int PULL_FREQ_PER_SEC = 100;
        public readonly int SAMPLE_RATE = 32000; // this should = CLIP_SAMPLES x PULL_FREQ_PER_SEC
        public readonly int CLIP_SAMPLES = 320;

        internal int _count;

        internal int _writeCount;
        internal int _readCount;

        internal RingBuffer<float> _audioBuffer;
        internal AudioClip _audioClip;

        private bool _startSignal;


        void Start()
        {
            //if (CheckAppId())
            {
                var aud = GetComponent<AudioSource>();
                if (aud == null)
                {
                    gameObject.AddComponent<AudioSource>();
                }
                SetupAudio(aud, "externalClip");
            }
        }

        // Update is called once per frame
        void Update()
        {
            PermissionHelper.RequestMicrophontPermission();
            PermissionHelper.RequestCameraPermission();
        }

        public override void Init(IRtcEngine engine, object rtclock)
        {
            RtcEngine = engine;
            RtcEngine.RegisterAudioFrameObserver(new AudioFrameObserver2(this), OBSERVER_MODE.RAW_DATA);
            RtcEngine.SetPlaybackAudioFrameParameters(SAMPLE_RATE, 1, RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_ONLY, 1024);
        }

        private void OnDestroy()
        {
            Debug.Log(name + " OnDestroy");
            if (RtcEngine != null)
            {
                RtcEngine.UnRegisterAudioFrameObserver();
            }
        }

        void SetupAudio(AudioSource aud, string clipName)
        {
            // //The larger the buffer, the higher the delay
            var bufferLength = SAMPLE_RATE / PULL_FREQ_PER_SEC * CHANNEL * 100; // 1-sec-length buffer
            _audioBuffer = new RingBuffer<float>(bufferLength, true);

            _audioClip = AudioClip.Create(clipName,
                CLIP_SAMPLES,
                CHANNEL, SAMPLE_RATE, true,
                OnAudioRead);
            aud.clip = _audioClip;
            aud.loop = true;
            aud.Play();
        }

        private void OnAudioRead(float[] data)
        {

            for (var i = 0; i < data.Length; i++)
            {
                lock (_audioBuffer)
                {
                    if (_audioBuffer.Count > 0)
                    {
                        data[i] = _audioBuffer.Get();
                        _readCount += 1;
                    }
                }
            }

            Debug.LogFormat("buffer length remains: {0}", _writeCount - _readCount);
        }

        internal static float[] ConvertByteToFloat16(byte[] byteArray)
        {
            var floatArray = new float[byteArray.Length / 2];
            for (var i = 0; i < floatArray.Length; i++)
            {
                floatArray[i] = BitConverter.ToInt16(byteArray, i * 2) / 32768f; // -Int16.MinValue
            }

            return floatArray;
        }
    }

    #region -- Agora Event ---

    internal class AudioFrameObserver2 : IAudioFrameObserver
    {
        private readonly CustomAudioObserver _agoraAudioRawData;
        private AudioParams _audioParams;


        internal AudioFrameObserver2(CustomAudioObserver agoraAudioRawData)
        {
            _agoraAudioRawData = agoraAudioRawData;
            _audioParams = new AudioParams();
            _audioParams.sample_rate = 16000;
            _audioParams.channels = 2;
            _audioParams.mode = RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_ONLY;
            _audioParams.samples_per_call = 1024;
        }

        public override bool OnRecordAudioFrame(string channelId, AudioFrame audioFrame)
        {
            Debug.Log("OnRecordAudioFrame-----------");
            return true;
        }

        public override bool OnPlaybackAudioFrame(string channelId, AudioFrame audioFrame)
        {
            Debug.Log("OnPlaybackAudioFrame-----------");
            if (_agoraAudioRawData._count == 1)
            {
                Debug.LogWarning("audioFrame = " + audioFrame);
            }
            var floatArray = CustomAudioObserver.ConvertByteToFloat16(audioFrame.RawBuffer);

            lock (_agoraAudioRawData._audioBuffer)
            {
                _agoraAudioRawData._audioBuffer.Put(floatArray);
                _agoraAudioRawData._writeCount += floatArray.Length;
                _agoraAudioRawData._count++;
            }
            return true;
        }

        public override int GetObservedAudioFramePosition()
        {
            Debug.Log("GetObservedAudioFramePosition-----------");
            return (int)(AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_PLAYBACK |
                AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_RECORD |
                AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_BEFORE_MIXING |
                AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_MIXED);
        }

        public override AudioParams GetPlaybackAudioParams()
        {
            Debug.Log("GetPlaybackAudioParams-----------");
            return this._audioParams;
        }

        public override AudioParams GetRecordAudioParams()
        {
            Debug.Log("GetRecordAudioParams-----------");
            return this._audioParams;
        }

        public override AudioParams GetMixedAudioParams()
        {
            Debug.Log("GetMixedAudioParams-----------");
            return this._audioParams;
        }

        public override bool OnPlaybackAudioFrameBeforeMixing(string channel_id,
                                                        uint uid,
                                                        AudioFrame audio_frame)
        {
            Debug.Log("OnPlaybackAudioFrameBeforeMixing-----------");
            return false;
        }

        public override bool OnPlaybackAudioFrameBeforeMixing(string channel_id,
                                                        string uid,
                                                        AudioFrame audio_frame)
        {
            Debug.Log("OnPlaybackAudioFrameBeforeMixing2-----------");
            return false;
        }
    }

    #endregion
}
