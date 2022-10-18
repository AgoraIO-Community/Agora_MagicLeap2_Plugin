using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;
using Agora.Util;

namespace agora_sample
{
    /// <summary>
    ///   The Video Render Manager manages the remote user's view transform.  
    /// In this example class, a referenceTransform is passed in for setting up the rotation
    /// value for the views.
    /// </summary>
    public class VideoRenderManager : IVideoRenderManager
    {
        // view control
        protected Dictionary<uint, VideoSurface> UserVideoDict = new Dictionary<uint, VideoSurface>();

        Transform SpawnPoint { get; set; }
        Transform ReferenceTransform { get; set; }
        string ChannelName = "";

        public VideoRenderManager(string channelName, Transform spawnPoint, Transform referenceTransform)
        {
            ChannelName = channelName;
            SpawnPoint = spawnPoint;
            ReferenceTransform = referenceTransform;
        }

        public virtual void MakeVideoView(uint uid)
        {
            GameObject go = GameObject.Find(uid.ToString());
            if (go != null)
            {
                return; // reuse
            }

            // create a GameObject and assign to this new user
            VideoSurface videoSurface = MakeImageSurface(uid.ToString());
            if (videoSurface != null)
            {
                // configure videoSurface
                if (uid == 0)
                {
                    videoSurface.SetForUser(uid);
                }
                else
                {
                    videoSurface.SetForUser(uid, ChannelName, VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
                }

                videoSurface.SetEnable(true);
                UserVideoDict[uid] = videoSurface;
            }
        }

        public virtual void UpdateVideoView(uint uid, int width, int height, int rotation)
        {
            Debug.LogFormat("uid:{3} OnVideoSizeChanged width = {0} height = {1} for rotation:{2}", width, height, rotation, uid);

            if (UserVideoDict.ContainsKey(uid))
            {
                GameObject go = UserVideoDict[uid].gameObject;
                Vector2 v2 = new Vector2(width, height);
                RawImage image = go.GetComponent<RawImage>();
                v2 = AgoraUIUtils.GetScaledDimension(width, height, 240f);

                if (rotation == 90 || rotation == 270)
                {
                    v2 = new Vector2(v2.y, v2.x);
                }
                image.rectTransform.sizeDelta = v2;
            }
        }

        public virtual void DestroyVideoView(uint uid)
        {
            GameObject go = GameObject.Find(uid.ToString());
            if (!ReferenceEquals(go, null))
            {
                Object.Destroy(go);
            }
        }

        // VIDEO TYPE 1: 3D Object
        //VideoSurface makePlaneSurface(string goName)
        //{
        //    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);

        //    if (go == null)
        //    {
        //        return null;
        //    }
        //    go.name = goName;
        //    // set up transform
        //    go.transform.Rotate(-90.0f, 0.0f, 0.0f);
        //    float yPos = UnityEngine.Random.Range(3.0f, 5.0f);
        //    float xPos = UnityEngine.Random.Range(-2.0f, 2.0f);
        //    go.transform.position = new Vector3(xPos, yPos, 0f);
        //    go.transform.localScale = new Vector3(0.25f, 0.5f, .5f);

        //    // configure videoSurface
        //    VideoSurface videoSurface = go.AddComponent<VideoSurface>();
        //    return videoSurface;
        //}

        // Video TYPE 2: RawImage
        protected virtual VideoSurface MakeImageSurface(string goName)
        {
            GameObject go = new GameObject();

            if (go == null)
            {
                return null;
            }

            go.name = goName;
            // to be renderered onto
            go.AddComponent<RawImage>();
            // make the object draggable
            // go.AddComponent<UIElementDrag>();
            if (SpawnPoint != null)
            {
                go.transform.SetParent(SpawnPoint);
            }
            else
            {
                Debug.Assert(SpawnPoint == null, "SpawnPoint is null ");
            }

            // set up transform
            go.transform.rotation = ReferenceTransform.rotation;
            go.transform.Rotate(0f, 0.0f, 180.0f);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            // configure videoSurface
            VideoSurface videoSurface = go.AddComponent<VideoSurface>();
            //videoSurface.OnTextureSizeModify += (int width, int height) =>
            //{
            //    float scale = (float)height / (float)width;
            //    videoSurface.transform.localScale = new Vector3(-5, 5 * scale, 1);
            //};

            return videoSurface;
        }

    }
}
