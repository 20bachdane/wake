using System.Collections;
using BeauUtil;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;
using BeauUtil.Debugger;
using UnityEditor.SceneManagement;
using Aqua.Debugging;
using ScriptableBake;

namespace Aqua.Editor
{
    public class SceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (EditorApplication.isPlaying)
                return;
            
            LoadSubscenes(scene);
            RemoveBootstrap(scene);
            RemoveDebug(scene);
            BakeScene(scene);
            StripEditorInfo(scene);
        }

        static private void RemoveBootstrap(Scene scene)
        {
            if (scene.buildIndex != 0)
            {
                BootParams[] bootstraps = GameObject.FindObjectsOfType<BootParams>();
                if (bootstraps.Length > 0)
                {
                    Debug.LogFormat("[SceneProcessor] Removing bootstraps from scene '{0}'...", scene.name);
                    foreach(var bootstrap in bootstraps)
                    {
                        GameObject.DestroyImmediate(bootstrap.gameObject);
                    }
                }
            }
        }

        static private void RemoveDebug(Scene scene)
        {
            if (EditorUserBuildSettings.development)
                return;
            
            DebugService debug = GameObject.FindObjectOfType<DebugService>();
            if (debug)
            {
                Debug.LogFormat("[SceneProcessor] Removing debug service from scene '{0}'...", scene.name);
                Baking.Destroy(debug.gameObject);
            }
        }

        static private void LoadSubscenes(Scene scene)
        {
            List<SubScene> allSubscenes = new List<SubScene>(4);
            scene.GetAllComponents<SubScene>(false, allSubscenes);
            if (allSubscenes.Count > 0)
            {
                Debug.LogFormat("[SceneProcessor] Loading {0} subscenes in scene '{1}'...", allSubscenes.Count, scene.name);
                using(Profiling.Time("load subscenes"))
                {
                    foreach(var subscene in allSubscenes)
                    {
                        SceneImportSettings importSettings = subscene;
                        GameObject.DestroyImmediate(subscene.gameObject);
                        EditorSceneManager.OpenScene(importSettings.ScenePath, OpenSceneMode.Additive);
                        Scene unitySubScene = EditorSceneManager.GetSceneByPath(importSettings.ScenePath);
                        foreach(var root in unitySubScene.GetRootGameObjects())
                        {
                            EditorSceneManager.MoveGameObjectToScene(root, scene);
                            SceneImportSettings.TransformRoot(root, importSettings);
                        }
                        if (importSettings.ImportLighting)
                        {
                            LightUtils.CopySettings(unitySubScene, scene);
                        }
                        EditorSceneManager.CloseScene(unitySubScene, true);
                    }
                }
            }
        }

        static private void BakeScene(Scene scene)
        {
            using(Profiling.Time("baking objects"))
            {
                Baking.BakeScene(scene);
            }
        }

        static private void StripEditorInfo(Scene scene)
        {
            List<IEditorOnlyData> allStrippable = new List<IEditorOnlyData>();
            scene.GetAllComponents<IEditorOnlyData>(true, allStrippable);
            if (allStrippable.Count > 0)
            {
                Debug.LogFormat("[SceneProcessor] Stripping editor data from {0} objects scene '{1}'...", allStrippable.Count, scene.name);
                using(Profiling.Time("stripping editor data"))
                {
                    foreach(var strippable in allStrippable)
                    {
                        // Debug.LogFormat("[SceneProcessor] ...stripping editor data from {0}", strippable.ToString());
                        strippable.ClearEditorOnlyData();
                    }
                }
            }
        }
    
        [MenuItem("Aqualab/DEBUG/Bake Scene")]
        static public void DEBUGBakeScene() {
            if (EditorApplication.isPlaying)
                return;

            Scene scene = EditorSceneManager.GetActiveScene();
            
            LoadSubscenes(scene);
            RemoveBootstrap(scene);
            RemoveDebug(scene);
            BakeScene(scene);
            StripEditorInfo(scene);
        }

        static public void DEBUGBakeSceneForManifest() {
            if (EditorApplication.isPlaying)
                return;

            Scene scene = EditorSceneManager.GetActiveScene();
            
            LoadSubscenes(scene);
            RemoveBootstrap(scene);
            RemoveDebug(scene);
            BakeScene(scene);
        }
    }
}