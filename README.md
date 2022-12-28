# AmplifyOcclusion-URP
https://github.com/AmplifyCreations/AmplifyOcclusion \
Ported in **URP 2022.2**, but might work in older versions.

### Requirements
* <b>com.unity.postprocessing</b> package installed! (uses StdLib.hlsl, I'd need to remove this dependency)

### How to use
* Add Amplify Occlusion in Volume component
* Add Amplify Occlusion RendererFeature in URP Renderer Asset
* Enable Depth Texture in URP Asset




https://user-images.githubusercontent.com/29812914/209881618-3d97f67a-c720-42a1-a656-4568823ec05f.mp4
>https://github.com/UnityTechnologies/open-project-1


https://user-images.githubusercontent.com/29812914/209586549-285353c3-1adf-4fdc-9627-e702273841e1.mp4
>Spider Controller in the video: https://github.com/PhilS94/Unity-Procedural-IK-Wall-Walking-Spider

## Known Issues
* Skybox is broken in 2020.3-2021.3
* Doesn't work with Multi-Camera setup (split-screen, etc)
* Doesn't work in scene view
* Temporal Filtering not supported
* GBuffer Normals not supported


