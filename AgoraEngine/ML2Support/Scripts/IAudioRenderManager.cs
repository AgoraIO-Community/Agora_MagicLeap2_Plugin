using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Agora.Rtc.Extended
{
    public abstract class IAudioRenderManager : MonoBehaviour
    {
        public abstract void Init(Agora.Rtc.IRtcEngine engine, object rtclock);
    }
}
