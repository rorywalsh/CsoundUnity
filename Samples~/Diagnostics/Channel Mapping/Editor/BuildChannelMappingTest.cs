using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Csound.Unity.Utilities.MonoBehaviours;
using Csound.Unity;

namespace Csound.Unity.Samples.AudioOutputPath.EditorTools
{
    /// <summary>
    /// Builds the channel mapping test scene: five cases that say where a Csound channel lands on a
    /// 5.1 output, and which ones never leave Csound.
    ///
    /// How to run it: Project Settings ▸ Audio ▸ Default Speaker Mode to <b>5.1</b>, open the scene,
    /// leave one case object active, press Play. The recorder on the camera writes a WAV to
    /// <c>persistentDataPath</c> after eight seconds and prints peak and RMS for every channel. The
    /// first thing to read in the console is the channel count: if it says 2, Unity fell back to
    /// stereo and the run proves nothing.
    ///
    /// Run from <b>CsoundUnity ▸ Tests ▸ Build Channel Mapping test scene</b>.
    /// </summary>
    public static class BuildChannelMappingTest
    {
        /// <summary>One of this sample's own files, used to find where it was imported.</summary>
        private const string AnchorCsd = "MappingTone2.csd";

        private static string _folder;

        private const int PathOnAudioFilterRead = 0;
        private const int PathIAudioGenerator   = 1;
        private const int PathRootOutput        = 2;

        [MenuItem("CsoundUnity/Tests/Build Channel Mapping test scene")]
        public static void Build()
        {
            _folder = FolderOf(AnchorCsd);
            if (_folder == null)
            {
                Debug.LogError($"[ChannelMapping test] {AnchorCsd} is not in this project: import the " +
                               "Diagnostics sample first.");
                return;
            }

            var scenePath = _folder + "/ChannelMapping.unity";

            if (!EditorUtility.DisplayDialog(
                    "Build the Channel Mapping test scene",
                    "This replaces " + scenePath + " with a freshly built scene.\n\n" +
                    "Any manual edits to it are lost.",
                    "Build", "Cancel"))
                return;

            AssetDatabase.Refresh();
            BuildScene(scenePath);
            AssetDatabase.Refresh();
        }

        private static void BuildScene(string scenePath)
        {
            var scene = NewScene();

            // Only the first case is active: one recording at a time, one file each.
            MakeCase("B1a", true, "MappingTone2.csd", PathOnAudioFilterRead, false,
                     "Stereo csd, 2D source.\n" +
                     "Centre, LFE and the surrounds silent means the duplication past nchnls is never heard in 2D.");

            MakeCase("B1b", false, "MappingTone2.csd", PathOnAudioFilterRead, true,
                     "Stereo csd, 3D source behind the listener. This is the one that decides.\n" +
                     "Signal in the surrounds means Unity's panner feeds them, so silencing the outputs past " +
                     "nchnls would silence this source. 660 Hz there is channel 2 duplicated.");

            MakeCase("B1c", false, "MappingTone6.csd", PathOnAudioFilterRead, false,
                     "Six channel csd, 2D source.\n" +
                     "If channels 3 to 6 are silent, the carrier clip's stereo cap is eating them: " +
                     "330, 392, 440 and 523 Hz never reach a speaker.");

            MakeCase("B1d", false, "MappingTone1.csd", PathOnAudioFilterRead, false,
                     "Mono csd, 2D source.\n" +
                     "Every channel Unity feeds carries the same 220 Hz, from Csound channel 1.");

            MakeCase("B1e", false, "MappingTone2.csd", PathIAudioGenerator, true,
                     "Stereo csd on IAudioGenerator, 3D source behind the listener.\n" +
                     "Signal in the surrounds means Unity pans downstream of the generator, and the " +
                     "reservation from B1b applies to this path too.");

            MakeCase("B1f", false, "MappingTone2.csd", PathIAudioGenerator, false,
                     "Stereo csd on IAudioGenerator, 2D source.\n" +
                     "Against B1a this says whether the paths are level with each other in 2D: " +
                     "220 Hz at 0.500 and 660 Hz at 0.250, the csd's own levels.");

            MakeCase("B1g", false, "MappingTone1.csd", PathIAudioGenerator, true,
                     "A mono csd on IAudioGenerator, 3D source behind the listener.\n" +
                     "The mono rule writes channel 1 into every channel of the generator's buffer, and " +
                     "Unity's panner sums them for a spatialized source: expect six times the level, " +
                     "the same way the MONO child does on this path.");

            // The runaway guard. Expect silence in the recording and one warning every couple of
            // seconds in the console: a sample that was let through would read 50.
            MakeCase("G1a", false, "RunawayTone.csd", PathOnAudioFilterRead, false,
                     "50 times full scale on OnAudioFilterRead.\n" +
                     "Silence, and a warning naming this instance and the level it reached.");

            MakeCase("G1b", false, "RunawayTone.csd", PathIAudioGenerator, false,
                     "50 times full scale on IAudioGenerator.\n" +
                     "Same: silence and one warning, from the path that could not warn at all before.");

            MakeCase("G1c", false, "NanTone.csd", PathOnAudioFilterRead, false,
                     "The square root of a negative signal, so every sample is not a number.\n" +
                     "Silence, and a warning that says so. The recorder shows no NaN in the file.");

            MakeCase("G1d", false, "RunawayTone.csd", PathRootOutput, false,
                     "50 times full scale on RootOutput, the path that had no guard at all.\n" +
                     "The recorder cannot see this path, so the console is the whole check here.");

            // CsoundUnityChild. The parent publishes two named channels and writes nothing to its own
            // output, so what a recording picks up came from the child.
            MakeChildCase("B2a", "MONO",   PathOnAudioFilterRead, false,
                          "A MONO child on OnAudioFilterRead, 2D.\n" +
                          "Against B2c: this path multiplies a MONO child by 0.5 and the other one does not.");

            MakeChildCase("B2c", "MONO",   PathIAudioGenerator,   false,
                          "The same MONO child on IAudioGenerator, 2D.\n" +
                          "Louder than B2a if the 0.5 is really missing here.");

            MakeChildCase("B2b", "STEREO", PathOnAudioFilterRead, false,
                          "A STEREO child on OnAudioFilterRead, 2D.\n" +
                          "Where do chanA (220 Hz) and chanB (660 Hz) land, and what reaches the speakers past the second?");

            MakeChildCase("B2d", "STEREO", PathIAudioGenerator,   false,
                          "The same STEREO child on IAudioGenerator, 2D.\n" +
                          "660 Hz in the centre, the LFE and the surrounds is the old duplication rule.");

            MakeChildCase("B2e", "MONO",   PathOnAudioFilterRead, true,
                          "A MONO child on OnAudioFilterRead, 3D behind the listener.\n" +
                          "Its carrier clip is mono, so this is the gain Unity's panner asks for.");

            MakeChildCase("B2f", "MONO",   PathIAudioGenerator,   true,
                          "The same MONO child on IAudioGenerator, 3D behind the listener.");

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[ChannelMapping test] built {scenePath}");
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

        /// <summary>
        /// Camera at the origin looking down +Z, so a source at negative Z is behind the listener.
        /// The recorder lives here, with the AudioListener: that is where Unity's final mix is.
        /// </summary>
        private static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = Vector3.zero;

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x33, 0x33, 0x33, 0xFF);
            camGo.AddComponent<AudioListener>();

            var rec = camGo.AddComponent<AudioOutputRecorder>();
            rec.recordSeconds    = 8;
            rec.startOnPlay      = true;
            rec.stopWhenFull     = true;
            rec.logReport        = true;
            // macOS reports a virtual 16 channel device as stereo, so asking the mixer directly is
            // what gets this scene its six channels.
            rec.forceSpeakerMode = true;
            rec.speakerMode      = AudioSpeakerMode.Mode5point1;

            return scene;
        }

        private static void MakeCase(string name, bool active, string csd, int path, bool behind, string expected)
        {
            var go = new GameObject(name);
            // Three metres behind the listener for the 3D cases, at the listener for the 2D ones.
            go.transform.position = behind ? new Vector3(0f, 0f, -3f) : Vector3.zero;

            var info = go.AddComponent<ChannelMappingCase>();
            info.caseName = name;
            info.expected = expected;

            var csound = go.AddComponent<CsoundUnity>();
            var guid   = AssetDatabase.AssetPathToGUID(_folder + "/" + csd);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[ChannelMapping test] {csd} not found in {_folder}");
                return;
            }

            csound.SetCsd(guid);
            ClearPresetFolders(csound);

            // _audioPath has no public setter: the inspector writes the serialised field, so does this.
            var so = new SerializedObject(csound);
            var p  = so.FindProperty("_audioPath");
            if (p != null) p.enumValueIndex = path;
            else if (path != PathOnAudioFilterRead) Debug.LogWarning("[ChannelMapping test] no _audioPath field: Unity below 6.");
            so.ApplyModifiedPropertiesWithoutUndo();

            var src = go.GetComponent<AudioSource>();
            if (src != null)
            {
                src.spatialBlend = behind ? 1f : 0f;
                src.dopplerLevel = 0f;
                // Inside the minimum distance there is no attenuation, so what the recording shows is
                // the panning and nothing else.
                src.rolloffMode  = AudioRolloffMode.Logarithmic;
                src.minDistance  = 20f;
                src.maxDistance  = 500f;
            }

            go.SetActive(active);
        }

        /// <summary>
        /// A parent that only publishes named channels, and a child that reads them. Both hang off one
        /// object, so enabling the case brings up the pair.
        /// </summary>
        private static void MakeChildCase(string name, string channels, int childPath, bool behind, string expected)
        {
            var root = new GameObject(name);

            var info = root.AddComponent<ChannelMappingCase>();
            info.caseName = name;
            info.expected = expected;

            // The parent: silent by itself, and left on the default path, which is not what these
            // cases are about.
            var parentGo = new GameObject("Parent");
            parentGo.transform.SetParent(root.transform);

            var parent = parentGo.AddComponent<CsoundUnity>();
            var guid   = AssetDatabase.AssetPathToGUID(_folder + "/ChildTone.csd");
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[ChannelMapping test] ChildTone.csd not found in {_folder}");
                return;
            }

            parent.SetCsd(guid);
            ClearPresetFolders(parent);

            var childGo = new GameObject("Child");
            childGo.transform.SetParent(root.transform);
            childGo.transform.position = behind ? new Vector3(0f, 0f, -3f) : Vector3.zero;

            var child = childGo.AddComponent<CsoundUnityChild>();
            child.csoundUnityGameObject = parentGo;
            child.AudioChannelsSetting  = channels == "MONO"
                ? CsoundUnityChild.AudioChannels.MONO
                : CsoundUnityChild.AudioChannels.STEREO;

            // chanA into the first slot, chanB into the second: the two are told apart by pitch.
            child.selectedAudioChannelIndexByChannel = channels == "MONO" ? new[] { 0 } : new[] { 0, 1 };

            // _audioPath is serialised and private here too, as on CsoundUnity.
            var so = new SerializedObject(child);
            var p  = so.FindProperty("_audioPath");
            if (p != null) p.enumValueIndex = childPath;
            else if (childPath != PathOnAudioFilterRead) Debug.LogWarning("[ChannelMapping test] no _audioPath on the child: Unity below 6.");
            so.ApplyModifiedPropertiesWithoutUndo();

            var src = childGo.GetComponent<AudioSource>();
            if (src != null)
            {
                src.spatialBlend = behind ? 1f : 0f;
                src.dopplerLevel = 0f;
                src.rolloffMode  = AudioRolloffMode.Logarithmic;
                src.minDistance  = 20f;
                src.maxDistance  = 500f;
            }

            root.SetActive(false);
        }

        /// <summary>
        /// Clears the preset Load and Save folders: assigning a csd fills them with a full path to
        /// this machine, which then travels inside the scene.
        /// </summary>
        private static void ClearPresetFolders(CsoundUnity csound)
        {
            if (csound == null) return;
            var so   = new SerializedObject(csound);
            var load = so.FindProperty("_currentPresetLoadFolder");
            var save = so.FindProperty("_currentPresetSaveFolder");
            if (load != null) load.stringValue = string.Empty;
            if (save != null) save.stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
