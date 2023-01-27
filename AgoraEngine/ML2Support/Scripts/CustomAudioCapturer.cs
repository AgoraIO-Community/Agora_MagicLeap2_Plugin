using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using Agora.Rtc;
using RingBuffer;
using UnityEngine.XR.MagicLeap;

namespace agora_sample
{
    /// <summary>
    /// The Custom Audio Capturer class uses Microphone audio source to
    /// capture voice input through ML2Audio.  The audio buffer is pushed
    /// constantly using the PushAudioFrame API in a thread. 
    /// </summary>
    public class CustomAudioCapturer : MonoBehaviour
    {
        [SerializeField]
        private AudioSource InputAudioSource = null;

        // Audio stuff
        public static int CHANNEL = 2;
        public const int
            SAMPLE_RATE = 48000; // Please do not change this value because Unity re-samples the sample rate to 48000.

        private const int RESCALE_FACTOR = 32767; // for short to byte conversion
        private int PUSH_FREQ_PER_SEC = 10;

        private RingBuffer<byte> _audioBuffer;
        private bool _startConvertSignal = false;

        private Thread _pushAudioFrameThread;
        private bool _pushAudioFrameThreadSignal = false;
        private int _count;

        private ML2BufferClip mlAudioBufferClip;

        const int AUDIO_CLIP_LENGTH_SECONDS = 60;

        IRtcEngine mRtcEngine;
        private System.Object _rtcLock = new System.Object();

        private void Awake()
        {
            StartMicrophone();
            StartCoroutine(CoStartRunning());
        }

        private void OnDestroy()
        {
            StopAudioPush();
        }

        IEnumerator CoStartRunning()
        {
            while (mRtcEngine == null)
            {
                yield return new WaitForFixedUpdate();
                mRtcEngine = RtcEngine.Instance;
            }
        }

        // Find and configure audio input, called during Awake
        private void StartMicrophone()
        {
            var captureType = MLAudioInput.MicCaptureType.VoiceCapture;
            if (!MLPermissions.CheckPermission(MLPermission.RecordAudio).IsOk)
            {
                Debug.LogError($"AudioCaptureExample.StartMicrophone() cannot start, {MLPermission.RecordAudio} not granted.");
                return;
            }
            mlAudioBufferClip = new ML2BufferClip(MLAudioInput.MicCaptureType.VoiceCapture, AUDIO_CLIP_LENGTH_SECONDS, MLAudioInput.GetSampleRate(captureType));
            mlAudioBufferClip.OnReceiveSampleCallback += HandleAudioBuffer;
        }

        public void StartPushAudioFrame()
        {
            StartCoroutine(CoStartPushingAudio());
        }

        private IEnumerator CoStartPushingAudio()
        {
            yield return new WaitUntil(() => mRtcEngine != null);
            var bufferLength = SAMPLE_RATE / PUSH_FREQ_PER_SEC * CHANNEL * 10000;
            _audioBuffer = new RingBuffer<byte>(bufferLength);
            _startConvertSignal = true;

            _pushAudioFrameThreadSignal = true;
            _pushAudioFrameThread = new Thread(PushAudioFrameThread);
            _pushAudioFrameThread.Start();
        }

        public void StopAudioPush()
        {
            _pushAudioFrameThreadSignal = false;
        }

        //get timestamp millisecond
        private double GetTimestamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return ts.TotalMilliseconds;
        }
        void PushAudioFrameThread()
        {
            var bytesPerSample = (int)BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE;
            var type = AUDIO_FRAME_TYPE.FRAME_TYPE_PCM16;
            var channels = CHANNEL;
            var samples = SAMPLE_RATE / PUSH_FREQ_PER_SEC;
            var samplesPerSec = SAMPLE_RATE;
            var freq = 1000 / PUSH_FREQ_PER_SEC;

            var audioFrame = new AudioFrame
            {
                bytesPerSample = BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE,
                type = type,
                samplesPerChannel = samples,
                samplesPerSec = samplesPerSec,
                channels = channels,
                RawBuffer = new byte[samples * bytesPerSample * CHANNEL],
                renderTimeMs = freq
            };

            double startMillisecond = GetTimestamp();
            long tick = 0;
            Debug.Log($"AGORA: PushAudioFrameThread started, timestamp:{startMillisecond}");
            while (_pushAudioFrameThreadSignal)
            {
                lock (_rtcLock)
                {
                    if (mRtcEngine == null)
                    {
                        Debug.LogWarning("AGORA: PushAudioFrameThread, RTCEngine is null");
                        break;
                    }

                    int nRet = -1;
                    lock (_audioBuffer)
                    {
                        if (_audioBuffer.Size > samples * bytesPerSample * CHANNEL)
                        {
                            for (var j = 0; j < samples * bytesPerSample * CHANNEL; j++)
                            {
                                audioFrame.RawBuffer[j] = _audioBuffer.Get();
                            }
                            nRet = mRtcEngine.PushAudioFrame(MEDIA_SOURCE_TYPE.AUDIO_PLAYOUT_SOURCE, audioFrame);
                            Debug.Log($"PushAudioFrame returns:{nRet} tick={tick} count={_count}");
                        }
                    }

                    if (nRet == 0)
                    {
                        tick++;
                        double nextMillisecond = startMillisecond + tick * freq;
                        double curMillisecond = GetTimestamp();
                        int sleepMillisecond = (int)Math.Ceiling(nextMillisecond - curMillisecond);
                        //Debug.Log("sleepMillisecond : " + sleepMillisecond);
                        if (sleepMillisecond > 0)
                        {
                            Thread.Sleep(sleepMillisecond);
                        }
                    }
                }
            }
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
            if (_count % 100 == 0)
            {
                Debug.Log($"AGORA: HandleAudioBuffer count:{_count}");
            }
        }
    }



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
}
