using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace agora_sample
{
    public abstract class IAudioCaptureManager : MonoBehaviour
    {
        public abstract void Init(Agora.Rtc.IRtcEngine engine, object rtclock);
        public abstract void StartAudioPush();
        public abstract void StopAudioPush();
    }
}
