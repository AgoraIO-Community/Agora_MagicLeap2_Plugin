using System.Threading;
using UnityEngine;
using Agora.Rtc;
using RingBuffer;
using System;

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
        private int PUSH_FREQ_PER_SEC = 100;

        private RingBuffer<byte> _audioBuffer;
        private bool _startConvertSignal = false;

        private Thread _pushAudioFrameThread;
        private bool _pushAudioFrameThreadSignal = false;
        private int _count;

        private bool _startSignal = false;
        private string _deviceMicrophone;

        const int AUDIO_CLIP_LENGTH_SECONDS = 60;

        int lastSample = 0;

        IRtcEngine mRtcEngine;

        private void Awake()
        {
            StartMicrophone();
        }

        private void FixedUpdate()
        {
            int pos = Microphone.GetPosition(null);
            int diff = pos - lastSample;

            if (diff > 0)
            {
                float[] samples = new float[diff * InputAudioSource.clip.channels];
                InputAudioSource.clip.GetData(samples, lastSample);
                HandleAudioBuffer(samples, InputAudioSource.clip.channels);
            }
            lastSample = pos;
        }


        private void OnDestroy()
        {
            StopAudioPush();
        }

        // Find and configure audio input, called during Awake
        private void StartMicrophone()
        {
            if (InputAudioSource == null)
            {
                InputAudioSource = GetComponent<AudioSource>();
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

        public void StartPushAudioFrame()
        {
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

        void PushAudioFrameThread()
        {
            var bytesPerSample = BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE;
            var type = AUDIO_FRAME_TYPE.FRAME_TYPE_PCM16;
            var channels = CHANNEL;
            var samples = SAMPLE_RATE / PUSH_FREQ_PER_SEC;
            var samplesPerSec = SAMPLE_RATE;
            var bufferLength = samples * (int)bytesPerSample * CHANNEL;
            var buffer = new byte[bufferLength];
            var freq = 1000 / PUSH_FREQ_PER_SEC;

            var tic = new TimeSpan(DateTime.Now.Ticks);

            mRtcEngine = RtcEngine.Instance;

            while (_pushAudioFrameThreadSignal)
            {
                if (!_startSignal)
                {
                    tic = new TimeSpan(DateTime.Now.Ticks);
                }

                var toc = new TimeSpan(DateTime.Now.Ticks);

                if (toc.Subtract(tic).Duration().Milliseconds >= freq)
                {
                    tic = new TimeSpan(DateTime.Now.Ticks);

                    for (var i = 0; i < 2; i++)
                    {
                        lock (_audioBuffer)
                        {
                            if (_audioBuffer.Size > bufferLength)
                            {
                                for (var j = 0; j < bufferLength; j++)
                                {
                                    buffer[j] = _audioBuffer.Get();
                                }

                                var audioFrame = new AudioFrame
                                {
                                    bytesPerSample = bytesPerSample,
                                    type = type,
                                    samplesPerChannel = samples / CHANNEL,
                                    samplesPerSec = samplesPerSec,
                                    channels = channels,
                                    RawBuffer = buffer,
                                    renderTimeMs = freq
                                };

                                mRtcEngine.PushAudioFrame(MEDIA_SOURCE_TYPE.AUDIO_RECORDING_SOURCE, audioFrame);
                            }
                        }
                    }
                }
            }
        }


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
            if (_count == 20) _startSignal = true;
        }
    }
}
