using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Agora.Rtc;
using RingBuffer;

namespace agora_sample
{
    /// <summary>
    /// The Custom AudioSink Player class receives audio frames from the
    /// Agora channel and applies the buffer to an AudioSource for playback.
    /// </summary>
    public class CustomAudioSinkPlayer : MonoBehaviour
    {
        private IRtcEngine mRtcEngine = null;

        public int CHANNEL = 1;
        public int SAMPLE_RATE = 44100;
        public int PULL_FREQ_PER_SEC = 100;
        public bool DebugFlag = false;

        int SAMPLES;
        int FREQ;
        int BUFFER_SIZE;

        private int writeCount = 0;
        private int readCount = 0;

        private RingBuffer<float> audioBuffer;
        private AudioClip _audioClip;


        private Thread _pullAudioFrameThread = null;
        private bool _pullAudioFrameThreadSignal = true;

        IntPtr BufferPtr { get; set; }

        // Start is called before the first frame update
        void Start()
        {
            SAMPLES = SAMPLE_RATE / PULL_FREQ_PER_SEC * CHANNEL;
            FREQ = 1000 / PULL_FREQ_PER_SEC;
            BUFFER_SIZE = SAMPLES * (int)BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE;

            StartCoroutine(CoStartRunning());
        }

        System.Collections.IEnumerator CoStartRunning()
        {
            while (mRtcEngine == null)
            {
                yield return new WaitForFixedUpdate();
                mRtcEngine = RtcEngine.Instance;
            }

            var aud = GetComponent<AudioSource>();
            if (aud == null)
            {
                aud = gameObject.AddComponent<AudioSource>();
            }
            KickStartAudio(aud, "externalClip");
        }

        void KickStartAudio(AudioSource aud, string clipName)
        {
            var bufferLength = SAMPLES * 100; // 1-sec-length buffer

            // allow overflow to prevent edge case 
            audioBuffer = new RingBuffer<float>(bufferLength, overflow: true);

            // Create and start the AudioClip playback, OnAudioRead will feed it
            _audioClip = AudioClip.Create(clipName,
                SAMPLE_RATE / PULL_FREQ_PER_SEC * CHANNEL, CHANNEL, SAMPLE_RATE, true,
                OnAudioRead);
            aud.clip = _audioClip;
            aud.loop = true;
            aud.Play();

            StartPullAudioThread();
        }

        void StartPullAudioThread()
        {
            if (_pullAudioFrameThread != null)
            {
                Debug.LogWarning("Stopping previous thread");
                _pullAudioFrameThread.Abort();
            }

            _pullAudioFrameThread = new Thread(PullAudioFrameThread);
            //_pullAudioFrameThread.Start("pullAudio" + writeCount);
            _pullAudioFrameThread.Start();
        }

        bool _paused = false;
        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                if (DebugFlag)
                {
                    Debug.Log("Application paused. AudioBuffer length = " + audioBuffer.Size);
                    Debug.Log("PullAudioFrameThread state = " + _pullAudioFrameThread.ThreadState + " signal =" + _pullAudioFrameThreadSignal);
                }

                // Invalidate the buffer
                _pullAudioFrameThread.Abort();
                _pullAudioFrameThread = null;
                _paused = true;
            }
            else
            {
                if (_paused) // had been paused, not from starting up
                {
                    Debug.Log("Resuming PullAudioThread");
                    audioBuffer.Clear();
                    StartPullAudioThread();
                }
            }
        }


        void OnDestroy()
        {
            Debug.Log("OnApplicationQuit");
            _pullAudioFrameThreadSignal = false;
            audioBuffer.Clear();
            if (BufferPtr != IntPtr.Zero)
            {
                Debug.LogWarning("cleanning up IntPtr buffer");
                Marshal.FreeHGlobal(BufferPtr);
                BufferPtr = IntPtr.Zero;
            }
        }

        private void PullAudioFrameThread()
        {
            var avsync_type = 0;
            var bytesPerSample = 2;
            var type = AUDIO_FRAME_TYPE.FRAME_TYPE_PCM16;
            var channels = CHANNEL;
            var samples = SAMPLE_RATE / PULL_FREQ_PER_SEC * CHANNEL;
            var samplesPerSec = SAMPLE_RATE;
            var buffer = new byte[samples * bytesPerSample];
            // var freq = 1000 / PULL_FREQ_PER_SEC;

            // BufferPtr = Marshal.AllocHGlobal(BUFFER_SIZE);

            var tic = new TimeSpan(DateTime.Now.Ticks);

            var byteArray = new byte[BUFFER_SIZE];
            long pullCount = 0;


            AudioFrame audioFrame = new AudioFrame(
             type, samples, BYTES_PER_SAMPLE.TWO_BYTES_PER_SAMPLE, channels, samplesPerSec, buffer, 0, avsync_type);
            BufferPtr = Marshal.AllocHGlobal(samples * bytesPerSample * channels);
            audioFrame.buffer = BufferPtr;

            while (_pullAudioFrameThreadSignal)
            {
                var toc = new TimeSpan(DateTime.Now.Ticks);
                if (toc.Subtract(tic).Duration().Milliseconds >= FREQ)
                {
                    tic = new TimeSpan(DateTime.Now.Ticks);
                    int rc = mRtcEngine.PullAudioFrame(audioFrame);

                    if (rc < 0)
                    {
                        if (pullCount % 1000 == 0 && pullCount < 100000)
                        {
                            Debug.LogWarning("PullAudioFrame returns " + rc);
                        }
                        pullCount++;
                        continue;
                    }

                    Marshal.Copy(audioFrame.buffer, byteArray, 0, BUFFER_SIZE);

                    var floatArray = ConvertByteToFloat16(byteArray);
                    lock (audioBuffer)
                    {
                        audioBuffer.Put(floatArray);
                    }

                    writeCount += floatArray.Length;
                    if (DebugFlag) Debug.Log("PullAudioFrame rc = " + rc + " writeCount = " + writeCount);
                }

            }

            if (BufferPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(BufferPtr);
                BufferPtr = IntPtr.Zero;
            }

            Debug.Log("Done running pull audio thread");
        }

        private static float[] ConvertByteToFloat16(byte[] byteArray)
        {
            var floatArray = new float[byteArray.Length / 2];
            for (var i = 0; i < floatArray.Length; i++)
            {
                floatArray[i] = BitConverter.ToInt16(byteArray, i * 2) / 32768f; // -Int16.MinValue
            }

            return floatArray;
        }

        // This Monobehavior method feeds data into the audio source
        private void OnAudioRead(float[] data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                lock (audioBuffer)
                {
                    if (audioBuffer.Count > 0)
                    {
                        data[i] = audioBuffer.Get();
                    }
                    else
                    {
                        // no data
                        data[i] = 0;
                    }
                }

                readCount += 1;
            }

            if (DebugFlag)
            {
                Debug.LogFormat("buffer length remains: {0}", writeCount - readCount);
            }
        }
    }
}
