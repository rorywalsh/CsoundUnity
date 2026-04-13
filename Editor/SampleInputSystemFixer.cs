using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Csound.Unity.Editor
{
    /// <summary>
    /// Fixes imported CsoundUnity sample scenes when the project uses Unity's new
    /// Input System package.
    ///
    /// Sample scenes are authored with the legacy <c>StandaloneInputModule</c> for
    /// maximum compatibility.  When they are imported into a project that has switched
    /// to the new Input System (<c>ENABLE_INPUT_SYSTEM</c> defined and
    /// <c>ENABLE_LEGACY_INPUT_MANAGER</c> not defined), the EventSystem in each scene
    /// must use <c>InputSystemUIInputModule</c> instead, otherwise Unity logs errors
    /// and UI interaction does not work.
    ///
    /// This postprocessor detects newly imported <c>.unity</c> assets under any
    /// <c>Samples</c> folder and, when the new Input System is active, swaps the
    /// input module automatically.
    /// </summary>
    public class SampleInputSystemFixer : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var scenesToFix = importedAssets
                .Where(a => a.EndsWith(".unity") && a.Contains("/Samples/CsoundUnity/"))
                .ToList();

            if (scenesToFix.Count == 0) return;

            // Defer execution outside the import pipeline so we can safely open scenes.
            EditorApplication.delayCall += () => FixScenes(scenesToFix);
#endif
        }

        /// <summary>
        /// Manually fix all CsoundUnity sample scenes already present in the project.
        /// Useful when scenes were imported before this fixer existed, or after switching
        /// the project to the new Input System.
        /// </summary>
        [MenuItem("CsoundUnity/Fix Sample Scenes for New Input System")]
        static void FixAllSampleScenesMenu()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Samples/CsoundUnity" });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("CsoundUnity", "No sample scenes found under Assets/Samples.", "OK");
                return;
            }
            FixScenes(paths);
#else
            EditorUtility.DisplayDialog("CsoundUnity",
                "This project is not using the new Input System exclusively — nothing to fix.", "OK");
#endif
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static void FixScenes(List<string> scenePaths)
        {
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputModuleType == null)
            {
                // Input System package installed but assembly not yet compiled — skip.
                return;
            }

            var fixed_ = new List<string>();

            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool modified = false;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
                    {
                        // Skip if already using the new module.
                        if (es.GetComponent(inputModuleType) != null) continue;

                        var standalone = es.GetComponent<UnityEngine.UI.StandaloneInputModule>();
                        if (standalone == null) continue;

                        Object.DestroyImmediate(standalone, true);
                        es.gameObject.AddComponent(inputModuleType);
                        modified = true;
                    }
                }

                if (modified)
                {
                    EditorSceneManager.SaveScene(scene);
                    fixed_.Add(scenePath);
                }

                EditorSceneManager.CloseScene(scene, true);
            }

            if (fixed_.Count > 0)
                Debug.Log($"[CsoundUnity] Updated {fixed_.Count} sample scene(s) to use " +
                          $"InputSystemUIInputModule:\n{string.Join("\n", fixed_)}");
        }
#endif
    }
}
