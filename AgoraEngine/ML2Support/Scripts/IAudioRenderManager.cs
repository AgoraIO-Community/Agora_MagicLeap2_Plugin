using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace agora_sample
{
    public abstract class IAudioRenderManager : MonoBehaviour
    {
        public abstract void Init(Agora.Rtc.IRtcEngine engine);
    }
}
