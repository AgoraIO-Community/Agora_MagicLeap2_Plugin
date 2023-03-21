namespace Agora.Rtc.Extended
{
    /// <summary>
    ///   This interface declares the neccessary methods for managing the views for video streaming.
    /// </summary>
    public interface IVideoRenderManager
    {
        void DestroyVideoView(uint uid);
        void MakeVideoView(uint uid);
        void UpdateVideoView(uint uid, int width, int height, int rotation);
    }
}
