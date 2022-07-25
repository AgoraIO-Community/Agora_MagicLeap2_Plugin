using System.Threading;
using UnityEngine;
using agora_gaming_rtc;
using RingBuffer;
using System;

namespace agora_sample
{
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

        void FixedUpdate()
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
            var bytesPerSample = 2;
            var type = AUDIO_FRAME_TYPE.FRAME_TYPE_PCM16;
            var channels = CHANNEL;
            var samples = SAMPLE_RATE / PUSH_FREQ_PER_SEC;
            var samplesPerSec = SAMPLE_RATE;
            var buffer = new byte[samples * bytesPerSample * CHANNEL];
            var freq = 1000 / PUSH_FREQ_PER_SEC;

            var tic = new TimeSpan(DateTime.Now.Ticks);

            mRtcEngine = IRtcEngine.QueryEngine();

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
                            if (_audioBuffer.Size > samples * bytesPerSample * CHANNEL)
                            {
                                for (var j = 0; j < samples * bytesPerSample * CHANNEL; j++)
                                {
                                    buffer[j] = _audioBuffer.Get();
                                }

                                var audioFrame = new AudioFrame
                                {
                                    bytesPerSample = bytesPerSample,
                                    type = type,
                                    samples = samples,
                                    samplesPerSec = samplesPerSec,
                                    channels = channels,
                                    buffer = buffer,
                                    renderTimeMs = freq
                                };

                                mRtcEngine.PushAudioFrame(audioFrame);
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
                    if(_audioBuffer.Count <= _audioBuffer.Capacity - 2)
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
