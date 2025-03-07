using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

using UnityEngine;
using System.IO;
using UnityEditor.SearchService;
using UnityGLTF;
using UnityEngine.EventSystems;
using Unity.VisualScripting;

public class ExportAllScenes : MonoBehaviour
{
    // Launch with:
    // "/Applications/Unity/Hub/Editor/2022.3.57f1/Unity.app/Contents/MacOS/Unity"  -projectPath ~/work/github/UnityGLTF-Interactivity-Sample-Assets/Interactivity-2022.3/ -executeMethod ExportAllScenes.Load -exportpath ~/work/mytestdir21 -batchmode -nographics -quit -logfile -
    private static void Export(Transform[] transforms, UnityEngine.Object[] resources, bool binary, string sceneName)
    {
        try
        {
            var settings = GLTFSettings.GetOrCreateSettings();
            var exportOptions = new ExportContext(settings) { TexturePathRetriever = GLTFExportMenu.RetrieveTexturePath };
            var exporter = new GLTFSceneExporter(transforms, exportOptions);

            if (resources != null)
            {
                exportOptions.AfterSceneExport += (sceneExporter, _) =>
                {
                    foreach (var resource in resources)
                    {
                        if (resource is Material material)
                            sceneExporter.ExportMaterial(material);
                        if (resource is Texture2D texture)
                            sceneExporter.ExportTexture(texture, "unknown");
                        if (resource is Mesh mesh)
                            sceneExporter.ExportMesh(mesh);
                    }
                };
            }

            var path = settings.SaveFolderPath;

            if (!string.IsNullOrEmpty(path))
            {
                var ext = binary ? ".glb" : ".gltf";
                var resultFile = GLTFSceneExporter.GetFileName(path, sceneName, ext);
                settings.SaveFolderPath = path;
                
                if (binary)
                    exporter.SaveGLB(path, sceneName);
                else
                    exporter.SaveGLTFandBin(path, sceneName);

                Debug.Log("Exported to " + resultFile);
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Had exception {e}");
        }
    }

    private static string GetCmdArgValue(string name)
    {
        string _name = "-" + name;
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(args, a => a == _name);
        if(i >= 0 && i < args.Length - 1)
        {
            return args[i + 1];
        }
        return null;
    }

    public static void Load()
    {
        List<UnityEngine.SceneManagement.Scene> allScenes = new List<UnityEngine.SceneManagement.Scene>();

        var info = new DirectoryInfo("Assets/Test Scenes/");
        var files = info.GetFiles();
        bool first = true;
        
        foreach(var f in files)
        {
            if(f.Name.EndsWith(".unity"))
            {
                Debug.Log($"Adding scene [{f.Name}]");
                var s = EditorSceneManager.OpenScene(f.FullName, first ? OpenSceneMode.Single : OpenSceneMode.Additive);      
                first = false;
                allScenes.Add(s);
            }
        }

        string exportPath = GetCmdArgValue("exportpath");

        var settings = GLTFSettings.GetOrCreateSettings();

        string prevSettingsPath = settings.SaveFolderPath;

        if(!string.IsNullOrEmpty(exportPath))
        {           
            settings.SaveFolderPath = exportPath;
        }

        var path = settings.SaveFolderPath;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        Debug.Log($"Exporting to {path}...");

        foreach(var s in allScenes)
        {
            var gameObjects = s.GetRootGameObjects();
            var transforms = Array.ConvertAll(gameObjects, gameObject => gameObject.transform);

            Export(transforms, null, true, s.name);
        }

        if(prevSettingsPath != null)
        {
            settings.SaveFolderPath = prevSettingsPath;
        }

        Debug.Log($"Complete ExportAllScenes.Load");
    }
}
