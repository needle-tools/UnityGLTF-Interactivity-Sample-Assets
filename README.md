# UnityGLTF Interactivity Sample Assets

Unity projects used to author and export the `KHR_interactivity` sample models and test assets that are published in the Khronos [glTF-Test-Assets-Interactivity](https://github.com/KhronosGroup/glTF-Test-Assets-Interactivity) repository.

Interactive behaviour is authored with Unity Visual Scripting and exported to glTF with [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF), which contains the `KHR_interactivity` exporter. Importing `KHR_interactivity` into Unity is not supported yet.

## Repository layout

| Path | Contents |
|-|-|
| `Interactivity-2022.3/` | Main Unity project (Unity 2022.3.62f2). |
| `Interactivity-6000.0/` | The same setup for Unity 6 (6000.0.65f1). |
| `glTF-Interactivity-Scenes/` | Local package `com.needle.gltf-interactivity-scenes`: sample scenes, the showcase models and the sample exporter. |
| `glTF-Interactivity-Tests/` | Local package `com.needle.gltf-interactivity-tests`: test generators and the test exporter. |
| `UnityGLTF/` | Submodule: [KhronosGroup/UnityGLTF](https://github.com/KhronosGroup/UnityGLTF), including the interactivity plugin. |
| `glTF-Test-Assets-Interactivity/` | Submodule: [KhronosGroup/glTF-Test-Assets-Interactivity](https://github.com/KhronosGroup/glTF-Test-Assets-Interactivity), the export target for models and tests. |

Both Unity projects reference the two local packages and UnityGLTF through `file:` paths in `Packages/manifest.json`, so all projects share the same scenes and code.

### `glTF-Interactivity-Scenes`

- `Interactivity Repo Models/`: the showcase models published under `Models/` in the test assets repo (Calculator, Ghost, SnailRace, WhackAMole, ABeautifulGameInteractivity, PerformanceTest, …). `SampleModelIndex.asset` lists them along with their metadata (name, description, tags, license).
- `Test Scenes/`: scenes for trying out individual features and engine behaviour. They are not published.
- `Editor/`: the sample model exporter, the index and README writer, and scene builders.

### `glTF-Interactivity-Tests`

- `Khronos Test Export/`: `TestCreator` / `TestExporter` and the test cases themselves (`Core`, `OpTests`, `ExtraTests`, `UserInteraction`, `InterGlbCommunication`, `InvalidGraphs`, math tests, …). The `TestExporter.unity` scene holds the exporters that are run.
- `SpecInvalidGraphCases.md`: the invalid graph cases from the spec that the `InvalidGraphs` tests cover.

## Getting started

1. Clone with submodules:
   ```sh
   git clone --recursive <this repo>
   # or, in an existing clone:
   git submodule update --init --recursive
   ```
2. Open `Interactivity-2022.3` (or `Interactivity-6000.0`) in the matching Unity version.
3. Open `Edit > Project Settings > Visual Scripting` and click <kbd>Regenerate Nodes</kbd>. The generated nodes are not checked in.
4. Make sure the interactivity export plugins are enabled in `Assets/Resources/UnityGLTFSettings`.

## Exporting

Everything is exported from the **`Sample Scenes`** menu.

### Sample models: `Sample Scenes > Export Khronos Sample Models`

This opens a window listing every model in `SampleModelIndex`. Select the models you want and export. For each model the exporter writes the following into `glTF-Test-Assets-Interactivity/Models/<Name>/`:

- `glTF-Binary/<Name>.glb`
- `screenshot/screenshot.png`
- `README.md`

It also regenerates `model-index.json` and `Models-showcase.md` for all models, so a partial export still leaves the repository consistent. The output folder is set by `modelsRootPath` on the index asset (default `../glTF-Test-Assets-Interactivity/Models`, relative to the Unity project).

To add a new sample, create a scene and a `SampleModelDefinition` in a new `Interactivity Repo Models/<yyyyMMdd>-<Name>/` folder, then add the definition to `SampleModelIndex`.

### Tests: `Sample Scenes > Export Khronos Tests`

This opens `TestExporter.unity` and runs every `TestCreator` in it (overview tests, math tests and inter-GLB tests), using each exporter's *All in one* / *Individual* settings. The output goes to `glTF-Test-Assets-Interactivity/Tests/Interactivity`. The destination folder is stored in EditorPrefs (`GLTFTestExportPath`), and Unity asks for it once if it is not set yet.

`Sample Scenes > Export Khronos Invalid Graph Tests` exports the invalid graph cases separately.

### Other tools

- `Sample Scenes > Export All Samples`: exports every scene under `Assets/Test Scenes/` to a folder you pick. Objects tagged `BATCH_EXPORT` are also exported on their own. You can run this headless:
  ```sh
  Unity -projectPath Interactivity-2022.3 -executeMethod ExportAllScenes.Load -exportpath <dir> -batchmode -nographics -quit
  ```
- `glTF Interactivity > Create Chess Scene` / `Create Performance Test Scene`: these regenerate the scenes for the ABeautifulGameInteractivity and PerformanceTest samples.
- To export a single object manually, right-click it and choose <kbd>UnityGLTF > Export selected as GLB</kbd>.

## Viewing exported files

Open https://gltf-interactivity.needle.tools and load a GLB. It shows the running scene together with an editable view of its interactivity graph.

## Further reading

- [KHR_interactivity specification](https://github.com/KhronosGroup/glTF/blob/interactivity/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc)
- [glTF-Test-Assets-Interactivity](https://github.com/KhronosGroup/glTF-Test-Assets-Interactivity)
- [Interactivity Graph Authoring Tool](https://github.com/KhronosGroup/glTF-InteractivityGraph-AuthoringTool)
- [Khronos blog: glTF Interactivity specification released for public comment](https://www.khronos.org/blog/gltf-interactivity-specification-released-for-public-comment)
