using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Agora.Rtc.Extended
{
    /// <summary>
    ///   This interface declares the neccessary methods for managing the views for video streaming.
    /// </summary>
    public abstract class IVideoCaptureManager : MonoBehaviour
    {
        public abstract void Init(Agora.Rtc.IRtcEngine engine, object rtclock);
        public abstract void ConnectCamera();
        public abstract void DisconnectCamera();
    }
}
