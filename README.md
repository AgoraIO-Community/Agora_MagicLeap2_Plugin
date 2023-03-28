# Agora Plugins for MagicLeap2
This document describes the contents of the Agora Unity SDK and usage of the sample prefabs and scripts.

## SDK Contents
The Agora Unity SDK is based on the official Agora Video SDK for Unity, version 4.1.0.  

In additional to the original plugin contents, a ML2Demo folder is added for MagicLeap2 exclusively.  

Unless making Pull Request for changes, developer should just download the unity package from [the Release section](https://github.com/AgoraIO-Community/Agora_MagicLeap2_Plugin/releases), without cloning this project.

The ML2 Unity Editor and Android API requirements supersedes Agora's SDK requirements.  Please see the official MagicLeap guidelines on Unity development environment.
 
 
## Using Agora with ML2
### QuickStart with Prefab
Please see Agora_MagicLeap2_Plugin/AgoraEngine/ML2Support/ML2Demo.  Simply drag and drop the AgoraController prefab into your scene and fill out the information from the Inspector.  See the **AgoraRtcController** section for explanation on each field item.
![AgoraMLDemo_-_MagicLeap_Examples_-_Android_-_Unity_2022_2_3f1__OpenGL_4_1_](https://user-images.githubusercontent.com/1261195/228102306-b077345e-0a77-42f2-91fe-296b44497df5.jpg)

### Reusable Components
For the MagicLeap2, Application that uses Agora for native RTC support must use **custom video and audio** capturing and rendering techniques respectively.  The scripts under the *agora_sample* namespace are refactored components that enable low-code solution:

 - **CustomAudioCapturer**: this script captures the local user's audio via Unity's Microphone interface and push the raw audio into Agora SD-RTN.  This is required if the ML2 user is a broadcaster.
 - **CustomAudioSinkPlayer**: this script receives the remote users' audio data and play the stream on a AudioSource component.  This is required.
 - **CustomVideoCapturer**: this script captures the local user's camera view and send it into the Agora SD-RTN.  The code utilized ML2's MLCamera capabilities to capture the raw video data from the camera. There are options to be set in the Inspector to accommodate different capturing parameters, such as resolution and XR/Camera modes.  This component can be replaced with other capturing methods.  Use the *ShareScreen* method in the script as an example to push raw data that is capture using other means.
 - **VideoRenderManager**: this script provides basic 2D rendering for the remote users' video stream.  Developer should override the virtual methods *MakeVideoView()* and *UpdateVideoView()* to accommodate different UI design. 
 - **TokenClient**: both prefab and script are provided to enable token security for the RTC channel.  See the Tokens section for more details.
 - **AgoraRtcController**: this script integrates the above components and enables the Agora RTC functionalities.

## AgoraRtcController

The class AgoraRtcController provides an example on how to integrate the components to support RTC functionality within the ML2.  The script is attached to a prefab called "AgoraController".   Developer may simply drag and drop the prefab to a scene to use it.

![Screenshot 2023-03-27 1](https://user-images.githubusercontent.com/1261195/228102957-31b3a361-5b83-4602-ae10-550164b8f131.png)


The following tables explains the fields that should be filed in the Inspector.

|Name|Description  |Required|
|--|--|--|
| APP_ID |The AppID obtained from Agora developer console  |Yes|
| Use Token Client | Enable it if you plan to use the TokenClient prefab to handle RESTful Token requests|Yes|
|CHANNEL_NAME| The name of the channel to join. Note that this also affects TokenClient|Yes|
|Spawn Point| the position where the remote user's view should be rendered|No
|Log Text| UI Text that can be used for on screen output|No
|New User View|a RawImage that shows the new user's video| No
|Mute Local Button|A Button that enables/disables local user's audio|No
|Mute Remote Button|A Button that enables/disables remote user's audio|No
|Custom Video Capture |The Video Capture component, see **CustomVideoCapturer.cs**|Yes
|Custom Audio Sink|The audio sink component, see **CustomAudioSinkPlayer.cs**| Yes
|Custom Audio Capture|The audio capture component, see **CustomAudioCapturer.cs**|Yes

Lastly, there is a **VideoRenderManager** class that implements the view placement of user video streams.  It is recommended to implement the IVideoRenderManager interface for your own applications design.


## Tokens ##
Agora recommends the use of tokens to add security for the channel communications. Please see [the online documentation](https://docs.agora.io/en/video-calling/develop/authentication-workflow?platform=unity) for better understanding.  In this SDK, refactored component **TokenClient** simplifies the steps.  You should use a corresponding Token Server established for working with the client.  The sample Go-Lang project can be found in [this Agora project repo](https://github.com/AgoraIO-Community/agora-token-service).   From this repo's main page, you should find choice buttons that help you to deploy the project to different server instances.

As an example a chosen endpoint is "https://agoraml2test.herokuapp.com/".  Copy and paste that to field in the TokenClient component.

![Screen Shot 2022-06-13 2](https://user-images.githubusercontent.com/1261195/173483945-e15c85cf-5532-4b31-8618-afcb40357720.png)

[Recommendation] For POC development, you can just skip the token client/server implementation and focus on the RTC logic first.  In *AgoraRtcController*, set UseToken to *false* and use a test-mode AppID for the integration.  See the*AgoraRtcController* section for reference.


## Resources
- [Online Agora Video SDK for Unity Documentation](https://docs.agora.io/en/Video/landing-page?platform=Unity)
- Check our  [FAQ](https://docs.agora.io/en/faq)  to see if your issue has been covered.
- [Unity SDK related tutorials](https://www.agora.io/en/blog/?s=unity&post_type=post)
- [MagicLeap 2 Forum](https://forum.magicleap.cloud/)

## License

MIT license.
