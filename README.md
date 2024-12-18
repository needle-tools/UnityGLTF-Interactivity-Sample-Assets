# UnityGLTF Interactivity Plugin

> [!WARNING]
> **This code is currently undergoing a lot of changes. Feel free to test and open issues, but please don't use it in production. Both the specification and the implementation will continue to change.**

## How to use this repository

#### Downloading the code
1. Clone this repository with a git client
2. Ensure that all submodules are cloned as well: [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF) and the [UnityGLTF Interactivity Plugin](https://github.com/needle-tools/UnityGLTF-Interactivity)).

#### Opening and preparing the project
3. Open the `Interactivity-2022.3` project. It currently uses Unity 2022.3.46f1.
4. Open `Edit > Project Settings > Visual Scripting`
5. Click <kbd>Regenerate Nodes</kbd>
6. Ensure that the KHR Interactivity Plugin is enabled in `Resources/UnityGLTFSettings`.

#### Opening a scene and exporting an interactive GLB file
7. Open Test Scenes – for example, `Assets/Test Scenes/TrafficLight.unity`
8. Some test scenes contain multiple variants of the same scene. Select the variant you want to export, for example `TrafficLight_Simple`.
9. Right-click on it and select <kbd>UnityGLTF/Export selected as GLB</kbd>
10. Choose and confirm where to store the exported GLB file.

#### Running the interactive GLB in your browser
12. Open https://gltf-interactivity.needle.tools in your browser
13. Scroll down, ensure the Babylon Engine is selected.
14. Pick your exported GLB file in the <kbd>Upload</kbd> selector.
15. The interactive scene will be displayed at the bottom of the screen, and the graph will be displayed at the top.
16. The graph is also editable when changes are needed.

## Further reading

- [KHR_interactivity Spec Proposal](https://github.com/KhronosGroup/glTF/blob/interactivity/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc)
- [Khronos Group – announcement on blog](https://www.khronos.org/blog/gltf-interactivity-specification-released-for-public-comment)
- [Interactivity Graph Authoring Tool](https://github.com/KhronosGroup/glTF-InteractivityGraph-AuthoringTool/tree/initial-work-merge)
- [YouTube Video](https://www.youtube.com/watch?v=-XLOkDiAYkQ)
- [Google IO Announcement regarding KHR_interactivity](https://developers.googleblog.com/en/google-ar-at-io-2024-new-geospatial-ar-features-and-more/)
