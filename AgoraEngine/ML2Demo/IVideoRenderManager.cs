namespace agora_sample
{
    public interface IVideoRenderManager
    {
        void DestroyVideoView(uint uid);
        void MakeVideoView(uint uid);
        void UpdateVideoView(uint uid, int width, int height, int rotation);
    }
}