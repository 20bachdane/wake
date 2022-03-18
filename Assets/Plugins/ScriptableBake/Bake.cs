using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace ScriptableBake {

    /// <summary>
    /// Object baking utility.
    /// </summary>
    static public class Bake {

        public delegate void BakeDelegate(IBaked baked);

        #if UNITY_EDITOR

        /// <summary>
        /// Executes prior to any object baking.
        /// </summary>
        static public event BakeDelegate OnPreBake;

        /// <summary>
        /// Executes after all object baking.
        /// </summary>
        static public event BakeDelegate OnPostBake;

        #region Scene

        /// <summary>
        /// Bakes the current scene.
        /// </summary>
        static public void CurrentScene(BakeFlags flags = 0) {
            Scene(SceneManager.GetActiveScene(), flags);
        }

        /// <summary>
        /// Bakes scene components.
        /// </summary>
        static public void Scene(Scene scene, BakeFlags flags = 0) {
            IEnumerator iter = SceneAsync(scene, flags);
            using (iter as IDisposable) {
                while (iter.MoveNext()) ;
            }
        }

        /// <summary>
        /// Bakes the current scene asynchronously.
        /// Use this in a coroutine.
        /// </summary>
        static public IEnumerator CurrentSceneAsync(BakeFlags flags = 0) {
            return SceneAsync(SceneManager.GetActiveScene(), flags);
        }

        /// <summary>
        /// Bakes scene asynchronously.
        /// Use this in a coroutine.
        /// </summary>
        static public IEnumerator SceneAsync(Scene scene, BakeFlags flags = 0) {
            bool bIgnoreDisabled = (flags & BakeFlags.IgnoreDisabledObjects) != 0;

            List<IBaked> rootLocal = new List<IBaked>(16);
            GameObject[] roots = scene.GetRootGameObjects();
            List<IBaked> bakeComponents = new List<IBaked>(roots.Length * 4);
            for (int i = 0; i < roots.Length; i++) {
                GameObject root = roots[i];
                if (bIgnoreDisabled && !root.activeSelf) {
                    continue;
                }

                rootLocal.Clear();
                root.GetComponentsInChildren<IBaked>(!bIgnoreDisabled, rootLocal);
                bakeComponents.AddRange(rootLocal);
            }

            return Process(bakeComponents, "scene: " + scene.name, flags, null);
        }

        #endregion // Scene

        #region Assets

        /// <summary>
        /// Bakes custom assets.
        /// </summary>
        static public void Assets(BakeFlags flags = 0) {
            IEnumerator iter = AssetsAsync(flags);
            using (iter as IDisposable) {
                while (iter.MoveNext()) ;
            }
        }

        /// <summary>
        /// Bakes custom assets within the given directories.
        /// </summary>
        static public void Assets(string[] directories, BakeFlags flags = 0) {
            IEnumerator iter = AssetsAsync(directories, flags);
            using (iter as IDisposable) {
                while (iter.MoveNext()) ;
            }
        }

        /// <summary>
        /// Bakes custom assets asynchronously.
        /// Use this in a coroutine.
        /// </summary>
        static public IEnumerator AssetsAsync(BakeFlags flags = 0) {
            return AssetsAsync(null, flags);
        }

        /// <summary>
        /// Bakes custom assets within the given directories asynchronously.
        /// Use this in a coroutine.
        /// </summary>
        static public IEnumerator AssetsAsync(string[] directories, BakeFlags flags = 0) {
            string[] guids;
            if (directories != null && directories.Length > 0) {
                guids = AssetDatabase.FindAssets("t:ScriptableObject", directories);
            } else {
                guids = AssetDatabase.FindAssets("t:ScriptableObject");
            }

            List<IBaked> bakeAssets = new List<IBaked>(guids.Length);
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object[] objectsAtPath = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in objectsAtPath) {
                    IBaked baked = obj as IBaked;
                    if (baked != null) {
                        bakeAssets.Add(baked);
                    }
                }
            }

            return Process(bakeAssets, "ScriptableObjects", flags, null);
        }

        #endregion // Assets

        #region Prefabs

        // static public IEnumerator PrefabsAsync(string[] directories, BakeFlags flags = 0) {
        //     string[] guids;
        //     if (directories != null && directories.Length > 0) {
        //         guids = AssetDatabase.FindAssets("t:Prefab", directories);
        //     } else {
        //         guids = AssetDatabase.FindAssets("t:Prefab");
        //     }
        // }

        #endregion // Prefabs

        #region Hierarchy

        // Brought over from BeauUtil

        /// <summary>
        /// Flattens the hierarchy at this transform. Children will become siblings.
        /// </summary>
        static public void FlattenHierarchy(Transform transform, bool recursive = false) {
            if (recursive) {
                int placeIdx = transform.GetSiblingIndex() + 1;
                FlattenHierarchyRecursive(transform, transform.parent, ref placeIdx);
                return;
            }

            if (!Application.isPlaying) {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(transform);
                if (root != null)
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            Transform parent = transform.parent;
            Transform child;
            int childCount = transform.childCount;
            int siblingIdx = transform.GetSiblingIndex() + 1;
            while (childCount-- > 0) {
                child = transform.GetChild(0);
                child.SetParent(parent, true);
                child.SetSiblingIndex(siblingIdx++);
            }
        }

        static private void FlattenHierarchyRecursive(Transform transform, Transform parent, ref int siblingIndex) {
            if (!Application.isPlaying) {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(transform);
                if (root != null)
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            Transform child;
            int childCount = transform.childCount;
            while (childCount-- > 0) {
                child = transform.GetChild(0);
                child.SetParent(parent, true);
                child.SetSiblingIndex(siblingIndex++);
                FlattenHierarchyRecursive(child, parent, ref siblingIndex);
            }
        }

        #endregion // Hierarchy

        /// <summary>
        /// Destroys an object.
        /// </summary>
        static public void Destroy(UnityEngine.Object obj) {
            if (!Application.isPlaying) {
                GameObject.DestroyImmediate(obj);
            } else {
                GameObject.Destroy(obj);
            }
        }

        static private IEnumerator Process(List<IBaked> baked, string source, BakeFlags flags, Action<UnityEngine.Object> onModify) {
            bool bVerbose = (flags & BakeFlags.Verbose) != 0;
            bool bProgress = (flags & BakeFlags.ShowProgressBar) != 0;
            bool bError = false;

            baked.Sort((a, b) => a.Order.CompareTo(b.Order));

            if (bVerbose) {
                Debug.LogFormat("[Bake] Found {0} bakeable objects in {1}", baked.Count, source);
            }

            try {
                if (baked.Count > 0) {
                    if (OnPreBake != null) {
                        if (bProgress) {
                            if (EditorUtility.DisplayCancelableProgressBar("Baking objects", "Pre-bake step", 0)) {
                                yield break;
                            }
                        }
                        foreach (var component in baked) {
                            OnPreBake(component);
                            yield return null;
                        }
                    }

                    for(int i = 0; i < baked.Count; i++) {
                        IBaked bakedObj = baked[i];
                        UnityEngine.Object unityObj = bakedObj as UnityEngine.Object;
                        if (bProgress) {
                            if (EditorUtility.DisplayCancelableProgressBar("Baking objects", string.Format("Baking '{0}'", bakedObj.ToString()), (float) i / baked.Count)) {
                                yield break;
                            }
                        }
                        if (bVerbose) {
                            Debug.LogFormat("[Bake] ...baking '{0}'", bakedObj.ToString());
                        }

                        try {
                            if (bakedObj.Bake(flags)) {
                                if (unityObj) {
                                    EditorUtility.SetDirty(unityObj);
                                    onModify?.Invoke(unityObj);
                                } else {
                                    baked.RemoveAt(i--);
                                }
                            }
                        }
                        catch(Exception e) {
                            Debug.LogException(e);
                            bError = true;
                        }
                        yield return null;
                    }

                    if (OnPostBake != null) {
                        if (bProgress) {
                            if (EditorUtility.DisplayCancelableProgressBar("Baking objects", "Post-bake step", 1)) {
                                yield break;
                            }
                        }
                        foreach (var component in baked) {
                            OnPostBake(component);
                            yield return null;
                        }
                    }
                }
            } finally {
                if (bProgress) {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (bError) {
                throw new BakeException("Baking failed");
            }
        }

        #endif // UNITY_EDITOR
    }

    /// <summary>
    /// Exception indicating that baking failed at some point.
    /// </summary>
    public class BakeException : Exception {
        public BakeException(Exception inner)
            : base("Baking failed", inner)
        { }

        public BakeException(string msg)
            : base(msg)
        { }

        public BakeException(string msg, params object[] args)
            : base(string.Format(msg, args))
        { }
    }

    /// <summary>
    /// Identifies a bakeable component or asset.
    /// </summary>
    public interface IBaked {
        #if UNITY_EDITOR
        int Order { get; }
        bool Bake(BakeFlags flags);
        #endif // UNITY_EDITOR
    }

    /// <summary>
    /// Flags to modify bake behavior.
    /// </summary>
    public enum BakeFlags {

        // Disabled scene objects will be ignored.
        IgnoreDisabledObjects = 0x01,

        // This is for a build
        IsBuild = 0x02,

        // This is for a batch-mode build
        IsBatchMode = 0x04,

        // This will output info to debug
        Verbose = 0x08,

        // This will display a progress bar
        ShowProgressBar = 0x10
    }
}