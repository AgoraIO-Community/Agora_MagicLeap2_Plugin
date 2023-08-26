using System;
using System.Collections;
using UnityEngine;
using Agora.Rtc;
using RingBuffer;

#if ML2_ENABLE
using UnityEngine.XR.MagicLeap;
#endif

namespace Agora.Rtc.Extended
{
    /// <summary>
    /// The Custom Audio Capturer class uses Microphone audio source to
    /// capture voice input through ML2Audio.  The audio buffer is pushed
    /// constantly using the PushAudioFrame API in a thread. 
    /// </summary>
    public class CustomAudioCapturer : IAudioCaptureManager
    {
        // Audio stuff
        public static int CHANNEL = 1;
        public const int
            SAMPLE_RATE = 48000; // Please do not change this value because Unity re-samples the sample rate to 48000.

        private const int RESCALE_FACTOR = 32767; // for short to byte conversion
        private const int PUSH_FREQ_PER_SEC = 10;
        private const int SEND_INTERVAL = 1000 / PUSH_FREQ_PER_SEC;

        private RingBuffer<byte> _audioBuffer;
        private bool _startConvertSignal = false;
        private bool _nextSendOK = true;

        private bool _pushAudioFrameThreadSignal = false;
        private int _count;
        private long tick;

        const int AUDIO_CLIP_LENGTH_SECONDS = 60;

        IRtcEngine mRtcEngine;
        // private System.Object _rtcLock = new System.Object();
        double startMillisecond = 0;
        AudioFrame _audioFrame;
        int BufferLength { get; set; }

        private object _rtclock;

#if ML2_ENABLE
        private ML2BufferClip mlAudioBufferClip;
#else
        [SerializeField]
        private AudioSource InputAudioSource = null;
        private string _deviceMicrophone;
#endif

        private void Awake()
        {
            StartMicrophone();
        }

        private void OnDestroy()
        {
            StopAudioPush();
        }

        public override void Init(Agora.Rtc.IRtcEngine engine, object rtclock)
        {
            mRtcEngine = engine;
            _rtclock = rtclock;

            var bytesPerSample = (int)BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE;
            var samples = SAMPLE_RATE / PUSH_FREQ_PER_SEC;
            BufferLength = samples * bytesPerSample * CHANNEL;
            _audioFrame = new AudioFrame
            {
                bytesPerSample = BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE,
                type = AUDIO_FRAME_TYPE.FRAME_TYPE_PCM16,
                samplesPerChannel = SAMPLE_RATE / PUSH_FREQ_PER_SEC,
                samplesPerSec = SAMPLE_RATE,
                channels = CHANNEL,
                RawBuffer = new byte[BufferLength],
                renderTimeMs = 1000 / PUSH_FREQ_PER_SEC
            };
            AgoraLog.Log("BufferLength = " + BufferLength);
        }

        private void Update()
        {
            double nextMillisecond = startMillisecond + tick * SEND_INTERVAL;
            double curMillisecond = GetTimestamp();
            int sleepMillisecond = (int)Math.Ceiling(nextMillisecond - curMillisecond);
            _nextSendOK = (sleepMillisecond <= 0);

            if (_pushAudioFrameThreadSignal && mRtcEngine != null && _nextSendOK)
            {
                int nRet = -1;
                int j = 0;
                lock (_audioBuffer)
                {
                    if (_audioBuffer.Size > BufferLength)
                    {
                        for (; j < BufferLength; j++)
                        {
                            _audioFrame.RawBuffer[j] = _audioBuffer.Get();
                        }
                    }
                }
                if (j > 0) // has data to send
                {
                    lock (_rtclock)
                    {
                        nRet = mRtcEngine.PushAudioFrame(_audioFrame);
                    }
                    tick++;
                }
            }
        }

        // Find and configure audio input, called during Awake
        private void StartMicrophone()
        {
#if ML2_ENABLE
            var captureType = MLAudioInput.MicCaptureType.VoiceCapture;
            if (!MLPermissions.CheckPermission(MLPermission.RecordAudio).IsOk)
            {
                Debug.LogError($"AudioCaptureExample.StartMicrophone() cannot start, {MLPermission.RecordAudio} not granted.");
                return;
            }
            mlAudioBufferClip = new ML2BufferClip(MLAudioInput.MicCaptureType.VoiceCapture, AUDIO_CLIP_LENGTH_SECONDS, MLAudioInput.GetSampleRate(captureType));
            mlAudioBufferClip.OnReceiveSampleCallback += HandleAudioBuffer;
#else
            StartMicrophone1();
#endif
        }

#if !ML2_ENABLE
        // Find and configure audio input, called during Awake
        private void StartMicrophone1()
        {
            if (InputAudioSource == null)
            {
                InputAudioSource = gameObject.AddComponent<AudioSource>();
            }

            // Use the first detected Microphone device.
            if (Microphone.devices.Length > 0)
            {
                _deviceMicrophone = Microphone.devices[0];
            }

            // If no microphone is detected, exit early and log the error.
            if (string.IsNullOrEmpty(_deviceMicrophone))
            {
                Debug.LogError("Error: HelloVideoAgora.deviceMicrophone could not find a microphone device, disabling script.");
                enabled = false;
                return;
            }

            InputAudioSource.loop = true;
            InputAudioSource.clip = Microphone.Start(_deviceMicrophone, true, AUDIO_CLIP_LENGTH_SECONDS, SAMPLE_RATE);
            CHANNEL = InputAudioSource.clip.channels;
            Debug.Log("StartMicrophone channels = " + CHANNEL);
        }
#endif

        public override void StartAudioPush()
        {
            tick = 0;
            startMillisecond = GetTimestamp();
            var bufferLength = SAMPLE_RATE / PUSH_FREQ_PER_SEC * CHANNEL * 10000;
            _audioBuffer = new RingBuffer<byte>(bufferLength);
            _startConvertSignal = true;
            _pushAudioFrameThreadSignal = true;
        }

        public override void StopAudioPush()
        {
            _pushAudioFrameThreadSignal = false;
        }

        //get timestamp millisecond
        private double GetTimestamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return ts.TotalMilliseconds;
        }

        private void HandleAudioBuffer(float[] data)
        {
            if (!_startConvertSignal) return;

            foreach (var t in data)
            {
                var sample = t;
                if (sample > 1) sample = 1;
                else if (sample < -1) sample = -1;

                var shortData = (short)(sample * RESCALE_FACTOR);
                var byteArr = new byte[2];
                byteArr = BitConverter.GetBytes(shortData);
                lock (_audioBuffer)
                {
                    if (_audioBuffer.Count <= _audioBuffer.Capacity - 2)
                    {
                        _audioBuffer.Put(byteArr[0]);
                        _audioBuffer.Put(byteArr[1]);
                    }
                }
            }

            _count += 1;
            //if (_count % 100 == 0)
            //{
            //    Debug.Log($"AGORA: HandleAudioBuffer count:{_count}");
            //}
        }

        // This method receives data from the audio source by the Unity engine
        private void HandleAudioBuffer(float[] data, int channels)
        {
            if (!_startConvertSignal) return;

            foreach (var t in data)
            {
                var sample = t;
                if (sample > 1) sample = 1;
                else if (sample < -1) sample = -1;

                var shortData = (short)(sample * RESCALE_FACTOR);
                var byteArr = new byte[2];
                byteArr = BitConverter.GetBytes(shortData);
                lock (_audioBuffer)
                {
                    if (_audioBuffer.Count <= _audioBuffer.Capacity - 2)
                    {
                        _audioBuffer.Put(byteArr[0]);
                        _audioBuffer.Put(byteArr[1]);
                    }
                }
            }

            _count += 1;
        }
    } /* end of CustomAudioCapturer class */

#if ML2_ENABLE
    /// <summary>
    ///   Extending BufferClip class for callback function
    /// </summary>
    public class ML2BufferClip : MLAudioInput.BufferClip
    {
        public ML2BufferClip(MLAudioInput.MicCaptureType captureType, int lengthSec, int frequency) : this(captureType, (uint)lengthSec, (uint)frequency, (uint)MLAudioInput.GetChannels(captureType)) { }

        public ML2BufferClip(MLAudioInput.MicCaptureType captureType, uint samplesLengthInSeconds, uint sampleRate, uint channels)
            : base(captureType, samplesLengthInSeconds, sampleRate, channels) { }

        public event Action<float[]> OnReceiveSampleCallback;

        protected override void OnReceiveSamples(float[] samples)
        {
            base.OnReceiveSamples(samples);
            if (OnReceiveSampleCallback != null)
            {
                OnReceiveSampleCallback(samples);
            }
        }
    }
#endif

}
