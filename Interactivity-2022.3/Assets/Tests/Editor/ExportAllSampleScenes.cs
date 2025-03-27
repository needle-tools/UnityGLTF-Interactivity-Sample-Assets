using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;
using Tests.Editor;
using UnityEditor;
using UnityGLTF;

public static class ExportAllScenes
{
    [MenuItem("Sample Scenes/Export All Samples")]
    public static void ExportAllScenesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        
        string path = EditorPrefs.GetString("sampleScenesExportPath", "");
        path = EditorUtility.SaveFolderPanel("Select a folder to save the samples", path, "");
        if (string.IsNullOrEmpty(path))
            return;
        
        EditorPrefs.SetString("sampleScenesExportPath", path);

        ExportTo(path);
    }
    
    // Launch with:
    // "/Applications/Unity/Hub/Editor/2022.3.57f1/Unity.app/Contents/MacOS/Unity"  -projectPath ~/work/github/UnityGLTF-Interactivity-Sample-Assets/Interactivity-2022.3/ -executeMethod ExportAllScenes.Load -exportpath ~/work/mytestdir21 -batchmode -nographics -quit -logfile -
    private static void Export(Transform[] transforms, bool binary, string sceneName, string path, bool resetRootTransforms = false)
    {
        if (string.IsNullOrEmpty(path))
            return;
        
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        
        try
        {
            var settings = GLTFSettings.GetOrCreateSettings();
            var exportOptions = new ExportContext(settings) { TexturePathRetriever = GLTFExportMenu.RetrieveTexturePath };

            var exporter = new GLTFSceneExporter(transforms, exportOptions);
            if (resetRootTransforms)
            {
                foreach (var rt in exporter.RootTransforms)
                {
                    rt.localPosition = Vector3.zero;
                }
            }
            
            var ext = binary ? ".glb" : ".gltf";
            var resultFile = GLTFSceneExporter.GetFileName(path, sceneName, ext);
            
            if (binary)
                exporter.SaveGLB(path, sceneName);
            else
                exporter.SaveGLTFandBin(path, sceneName);

            Debug.Log($"\t <color=#00FF00>Exported to </color> {resultFile}");
            
        }
        catch (Exception e)
        {
            Debug.Log($"\t <color=#0000FF> Had exception {e} </color>");
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
        string exportPath = GetCmdArgValue("exportpath");

        ExportTo(exportPath);
    }

    public static void ExportTo(string exportPath)
    {
        List<FileInfo> files = new List<FileInfo>();
        
        void ReadDirectory(DirectoryInfo info)
        {
            files.AddRange(info.GetFiles().Where( fInfo => fInfo.Name.EndsWith(".unity")));
            
            var directories = info.GetDirectories();
            foreach (var d in directories)
                ReadDirectory(d);
        }

        void FindBatchExports(Transform transform, List<Transform> batchExports)
        {
            if (transform.CompareTag("BATCH_EXPORT"))
                batchExports.Add(transform);

            for (int i = 0; i < transform.childCount; i++)
                FindBatchExports(transform.GetChild(i), batchExports);
        }
        
        string GetHierarchyName(Transform transform)
        {
            if (transform.parent == null)
                return transform.name;
            return GetHierarchyName(transform.parent) + "_" + transform.name;
        }
        
        var info = new DirectoryInfo("Assets/Test Scenes/");
        string fullScenePath = info.FullName; 
        ReadDirectory(info);
        
        Debug.Log($"Exporting to {exportPath}...");

        foreach(var f in files)
        {
            var s = EditorSceneManager.OpenScene(f.FullName, OpenSceneMode.Single);
            
            var gameObjects = s.GetRootGameObjects();
            var transforms = Array.ConvertAll(gameObjects, gameObject => gameObject.transform);

            var relativeSubPath = System.IO.Path.GetRelativePath(fullScenePath, f.Directory.FullName);
            var sceneExportPath = System.IO.Path.Combine(exportPath, relativeSubPath);
            Debug.Log($"<b><color=#F69012> Exporting scene </color> {s.name}</b>");
            
            foreach (var t in transforms)
            {
                var exportInterfaces = t.GetComponentsInChildren<IExportScene>(t);
                foreach (var i in exportInterfaces)
                    i.OnBeforeExporting();
            }
            
            Export(transforms, true, s.name, sceneExportPath, true);

            var batchExports = new List<Transform>();
            foreach (var tr in transforms)
                FindBatchExports(tr, batchExports);

            var batchExportPath = System.IO.Path.Combine(sceneExportPath, s.name);

            foreach (var batchExport in batchExports)
            {       
                var exportName = GetHierarchyName(batchExport);
                Debug.Log($"  <color=#F69012> Exporting batch transform: </color> {exportName}");
                Export(new []{batchExport}, true, exportName, batchExportPath, true);
            }
            
        }
        
        Debug.Log($"<color=#00FF00><b>Completed</b></color>");
        
        EditorUtility.RevealInFinder(exportPath);
    }
}
