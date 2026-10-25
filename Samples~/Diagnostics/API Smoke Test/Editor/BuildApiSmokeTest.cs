using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Csound.Unity;

// Not Samples.ApiSmokeTest: a namespace segment with the same name as the component would win when
// AddComponent<ApiSmokeTest>() is resolved, and the error says it is a namespace used like a type.
namespace Csound.Unity.Samples.SmokeTest.EditorTools
{
    /// <summary>
    /// Builds the API smoke test scene: one CsoundUnity, one orchestra written so that every public
    /// method under test has something to answer with, and the runner that calls them.
    /// <para>
    /// Run from <b>CsoundUnity ▸ Tests ▸ Build API smoke test scene</b>, then press Play: the console
    /// gets one report with a line per method, and the buttons on screen cover what only a person can
    /// accept.
    /// </para>
    /// </summary>
    public static class BuildApiSmokeTest
    {
        private const string Csd = "ApiSmokeTest.csd";

        [MenuItem("CsoundUnity/Tests/Build API smoke test scene")]
        public static void Build()
        {
            var folder = FolderOf(Csd);
            if (folder == null)
            {
                Debug.LogError($"[ApiSmokeTest] {Csd} is not in this project: import the Diagnostics sample first.");
                return;
            }

            var scenePath = folder + "/ApiSmokeTest.unity";

            if (!EditorUtility.DisplayDialog(
                    "Build the API smoke test scene",
                    "This replaces " + scenePath + " with a freshly built scene.\n\n" +
                    "Any manual edits to it are lost.",
                    "Build", "Cancel"))
                return;

            AssetDatabase.Refresh();

            var scene = NewScene();

            var go = new GameObject("API Smoke Test");

            var csound = go.AddComponent<CsoundUnity>();
            var guid = AssetDatabase.AssetPathToGUID(folder + "/" + Csd);

            csound.SetCsd(guid);

            // Assigning a csd fills the preset folders with a path to this machine, which then travels
            // inside the scene.
            var so   = new SerializedObject(csound);
            var load = so.FindProperty("_currentPresetLoadFolder");
            var save = so.FindProperty("_currentPresetSaveFolder");
            if (load != null) load.stringValue = string.Empty;
            if (save != null) save.stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<ApiSmokeTest>();

            var src = go.GetComponent<AudioSource>();
            if (src != null) src.spatialBlend = 0f;

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[ApiSmokeTest] built {scenePath}");

            AssetDatabase.Refresh();
        }


        /// <summary>
        /// The folder this sample was imported into, found from one of its own files. A sample lands
        /// under Assets/Samples/CsoundUnity/&lt;version&gt;/, so the path cannot be written here.
        /// </summary>
        private static string FolderOf(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + fileName, System.StringComparison.Ordinal))
                    return path.Substring(0, path.Length - fileName.Length - 1);
            }

            return null;
        }

        /// <summary>A camera and an AudioListener, and nothing else to look at.</summary>
        private static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = Vector3.zero;

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x33, 0x33, 0x33, 0xFF);
            camGo.AddComponent<AudioListener>();

            return scene;
        }
    }
}
