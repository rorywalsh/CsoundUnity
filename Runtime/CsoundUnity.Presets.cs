/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;


namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        #region PRESETS

        /// <summary>
        /// Create a CsoundUnityPreset from a presetName, csoundFileName and a list of CsoundChannelControllers
        /// </summary>
        /// <param name="presetName">Name of the preset (defaults to "CsoundUnityPreset" if empty).</param>
        /// <param name="csoundFileName">Name of the CSD file this preset is associated with.</param>
        /// <param name="channels">Channel controllers whose values will be deep-copied into the preset.</param>
        /// <returns>A new <see cref="CsoundUnityPreset"/> ScriptableObject instance.</returns>
        public static CsoundUnityPreset CreatePreset(string presetName, string csoundFileName, List<CsoundChannelController> channels)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();
            preset.name = preset.presetName = string.IsNullOrWhiteSpace(presetName) ? "CsoundUnityPreset" : presetName;
            preset.csoundFileName = csoundFileName;
            preset.channels = new List<CsoundChannelController>();
            foreach (var chan in channels)
            {
                var newChan = chan.Clone();
                preset.channels.Add(newChan);
            }
            return preset;
        }

        /// <summary>
        /// Create a CsoundUnityPreset from a presetName and presetData.
        /// <para>PresetData should be a CsoundUnityPreset in the JSON format.</para>
        /// <para>It will use the presetName parameter if not empty, 
        /// if presetName is empty it will use preset.presetName, 
        /// if also preset.presetName is empty it will use "CsoundUnityPreset" as a default name.
        /// </para>
        /// </summary>
        /// <param name="presetName">Name override; if empty the name embedded in <paramref name="presetData"/> is used.</param>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The deserialized preset, or <c>null</c> if the JSON could not be parsed.</returns>
        public static CsoundUnityPreset CreatePreset(string presetName, string presetData)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();

            // try and create a CsoundUnityPreset from presetData
            try
            {
                JsonUtility.FromJsonOverwrite(presetData, preset);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"Couldn't set Preset {presetName}, {ex.Message}");
                return null;
            }
            preset.name = preset.presetName = string.IsNullOrWhiteSpace(presetName) ?
                string.IsNullOrWhiteSpace(preset.presetName) ?
                    "CsoundUnityPreset" : preset.presetName : presetName;

            return preset;
        }

        /// <summary>
        /// Write a CsoundUnityPreset at the specified path inside the Assets folder.
        /// You can pass a full path, the Assets folder path will be extracted.
        /// </summary>
        /// <param name="preset">The preset ScriptableObject to write.</param>
        /// <param name="path">Target directory path; the Assets sub-path will be extracted automatically.</param>
        public static void WritePreset(CsoundUnityPreset preset, string path)
        {
#if UNITY_EDITOR
            // create target directory if it doesn't exist, defaulting to "Assets"
            if (string.IsNullOrWhiteSpace(path) || !path.Contains("Assets"))
            {
                Debug.LogWarning("CsoundUnityPreset scriptable object cannot be created outside of the project folder, " +
                    "defaulting to 'Assets'. Use the JSON format to save it outside of the Application.dataPath folder.");
                path = Application.dataPath;
            }

            // convert to a path inside the project
            var assetsIndex = path.IndexOf("Assets");

            if (assetsIndex < 0)
            {
                Debug.LogError("Error, couldn't find the Assets folder!");
                return;
            }

            path = path.Substring(assetsIndex, path.Length - assetsIndex);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fullPath = Path.Combine(path, $"{preset.presetName}.asset");//path + $"{preset.presetName}.asset";//
            if (AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(fullPath) != null)
            {
                var assetLength = ".asset".Length;
                var basePath = $"{fullPath.Substring(0, fullPath.Length - assetLength)}";
                var baseName = preset.presetName;
                var overwriteAction = new Action(() =>
                {
                    AssetDatabase.DeleteAsset(fullPath);
                    AssetDatabase.CreateAsset(preset, fullPath);
                    Debug.Log($"Overwriting CsoundUnityPreset at path {fullPath}");
                });
                var renameAction = new Action(() =>
                {
                    var count = 0;
                    while (AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(fullPath) != null)
                    {
                        preset.presetName = $"{baseName}_{count}";
                        fullPath = $"{basePath}_{count}.asset";
                        count++;
                    }

                    Debug.Log($"Saving CsoundUnityPreset at path {fullPath}");
                    AssetDatabase.CreateAsset(preset, fullPath);
                });

                var message = $"CsoundUnityPreset at {fullPath} already exists, overwrite or rename?";
                var res = EditorUtility.DisplayDialogComplex("Overwrite?", message, "Overwrite", "Cancel", "Rename");
                switch (res)
                {
                    case 0:
                        overwriteAction.Invoke();
                        break;
                    case 1:
                        // do nothing if cancel
                        break;
                    case 2:
                        renameAction.Invoke();
                        break;
                }
            }
            else
            {
                Debug.Log($"Creating new CsoundUnityPreset at {fullPath}");
                AssetDatabase.CreateAsset(preset, fullPath);
            }
            AssetDatabase.ImportAsset(fullPath);
            //AssetDatabase.SaveAssets();

#endif
        }

        /// <summary>
        /// Save a CsoundUnityPreset of this CsoundUnity instance at the specified path, using the specified presetName.
        /// The current csoundFileName and list of CsoundChannelControllers will be saved in the preset.
        /// If no presetName is given, a default one will be used.
        /// </summary>
        /// <param name="presetName">Name for the preset (defaults to "CsoundUnityPreset" if empty).</param>
        /// <param name="path">Target directory path inside the Assets folder; defaults to Assets root if null.</param>
        public void SavePresetAsScriptableObject(string presetName, string path = null)
        {
#if UNITY_EDITOR
            var preset = CreatePreset(presetName, this.csoundFileName, this.channels);
            WritePreset(preset, path);
#endif
        }

        /// <summary>
        /// Save the specified CsoundUnityPreset as JSON, at the specified path.
        /// If a file exists at the specified path, it will be overwritten if overwriteIfExisting is true.
        /// </summary>
        /// <param name="preset">The preset to serialize to JSON.</param>
        /// <param name="path">Target directory path; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public static void SavePresetAsJSON(CsoundUnityPreset preset, string path = null, bool overwriteIfExisting = false)
        {
            var fullPath = CheckPathForExistence(path, preset.presetName, overwriteIfExisting);
            var presetData = JsonUtility.ToJson(preset, true);
            try
            {
                Debug.Log($"Saving JSON preset at {fullPath}");
                File.WriteAllText(fullPath, presetData);
            }
            catch (IOException ex)
            {
                Debug.Log(ex.Message);
            }
#if UNITY_EDITOR
            if (fullPath.StartsWith(Application.dataPath))
            {
                var relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(relativePath);
            }
#endif
        }

        /// <summary>
        /// Save a preset as JSON from a list of CsoundChannelController, specifying the related CsoundFileName and the presetName.
        /// See <see cref="SavePresetAsJSON(CsoundUnityPreset, string, bool)">SavePresetAsJSON(CsoundUnityPreset, string, bool)</see>
        /// </summary>
        /// <param name="channels">Channel controllers to store in the preset.</param>
        /// <param name="csoundFileName">Name of the CSD file this preset is associated with.</param>
        /// <param name="presetName">Name for the preset.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public static void SavePresetAsJSON(List<CsoundChannelController> channels, string csoundFileName, string presetName, string path = null, bool overwriteIfExisting = false)
        {
            // Through CreatePreset, which clones every controller. Assigning the list straight in
            // handed the preset the caller's live controllers, so anything still holding that preset
            // kept editing it with every later channel write.
            var preset = CreatePreset(presetName, csoundFileName, channels);
            SavePresetAsJSON(preset, path, overwriteIfExisting);
        }

        /// <summary>
        /// Save a preset as JSON using CsoundChannelControllers and CsoundFileName from this CsoundUnity instance.
        /// See <see cref="SavePresetAsJSON(CsoundUnityPreset, string, bool)">SavePresetAsJSON(CsoundUnityPreset, string, bool)</see>
        /// </summary>
        /// <param name="presetName">Name for the preset.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public void SavePresetAsJSON(string presetName, string path = null, bool overwriteIfExisting = false)
        {
            var preset = CreatePreset(presetName, this.csoundFileName, this.channels);
            SavePresetAsJSON(preset, path, overwriteIfExisting);
        }

        /// <summary>
        /// Save a serialized copy of this CsoundUnity instance.
        /// Similar behaviour as saving a Unity Preset from the inspector of the CsoundUnity component, but this can be used at runtime.
        /// </summary>
        /// <param name="presetName">Name for the global preset file.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public void SaveGlobalPreset(string presetName, string path = null, bool overwriteIfExisting = false)
        {
            var presetData = JsonUtility.ToJson(this, true);
            var name = $"{presetName} {GLOBAL_TAG}";
            var fullPath = CheckPathForExistence(path, name, overwriteIfExisting);
            try
            {
                Debug.Log($"Saving global preset at {fullPath}");
                File.WriteAllText(fullPath, presetData);

            }
            catch (IOException ex)
            {
                Debug.Log(ex.Message);
            }
#if UNITY_EDITOR
            if (fullPath.StartsWith(Application.dataPath))
            {
                var relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(relativePath);
            }
#endif
        }

        /// <summary>
        /// Convert a JSON preset into a Scriptable Object preset to be written at the specified path.
        /// If path is empty the converted preset will be saved inside the Assets folder.
        /// </summary>
        /// <param name="path">Path to the source JSON preset file.</param>
        /// <param name="destination">Target directory inside the Assets folder where the ScriptableObject will be written.</param>
        public void ConvertPresetToScriptableObject(string path, string destination)
        {
#if UNITY_EDITOR
            LoadPreset(path, (preset) =>
            {
                WritePreset(preset, destination);
            });
#endif

        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance using a presetName and presetData.
        /// The set preset will be returned.
        /// <para>Preset data should represent a CsoundUnityPreset in JSON format.</para>
        /// If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.
        /// </summary>
        /// <param name="presetName">Name to assign to the preset (overrides the name in the JSON).</param>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The applied <see cref="CsoundUnityPreset"/>, or <c>null</c> if creation failed.</returns>
        public CsoundUnityPreset SetPreset(string presetName, string presetData)
        {
            var preset = CreatePreset(presetName, presetData);
            SetPreset(preset);
            return preset;
        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance using presetData. 
        /// <para>The set preset will be returned.
        /// The name of the preset will be the one found inside presetData, if not empty.
        /// Otherwise a default presetName will be used.</para>
        /// <para>Preset data should represent a CsoundUnityPreset in JSON format.</para>
        /// If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.
        /// </summary>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The applied <see cref="CsoundUnityPreset"/>, or <c>null</c> if creation failed.</returns>
        public CsoundUnityPreset SetPreset(string presetData)
        {
            return SetPreset("", presetData);
        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance.
        /// <para>If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.</para>
        /// </summary>
        /// <param name="preset">The preset to apply to this instance.</param>
        public void SetPreset(CsoundUnityPreset preset)
        {
            if (preset == null)
            {
                Debug.LogError("Couldn't load a null CsoundUnityPreset!");
                return;
            }
            if (this.csoundFileName != preset.csoundFileName)
            {
                Debug.LogError($"Couldn't set preset {preset.presetName} to this CsoundUnity instance {this.name}, " +
                    $"this instance uses csd: {this.csoundFileName}, the preset was saved with csd: {preset.csoundFileName} instead");
                return;
            }

            _currentPreset = preset;
            _currentPresetName = preset.presetName;
            SetChannels(preset.channels, true);
        }

        /// <summary>
        /// Set a global preset to this CsoundUnity instance using a presetName and presetData.
        /// <para>Preset data should represent a CsoundUnity instance in JSON format.</para>
        /// <para>This overrides the current content of this CsoundUnity instance.</para>
        /// This could have unintended consequences in certain situations. 
        /// You could need to disable this CsoundUnity GameObject and enable it again to restore the Csound internal state.
        /// <para>This updated CsoundUnity instance will be returned.</para>
        /// </summary>
        /// <param name="presetName">Name to assign (overrides the name embedded in the JSON).</param>
        /// <param name="presetData">JSON string of a full CsoundUnity instance serialization.</param>
        /// <returns>This <see cref="CsoundUnity"/> instance (for method chaining).</returns>
        public CsoundUnity SetGlobalPreset(string presetName, string presetData)
        {
            JsonUtility.FromJsonOverwrite(presetData, this);
            // update the serialized channels 
            SetChannels(this.channels, true);
            // Only the name: a global preset is a snapshot of the whole component, not a
            // CsoundUnityPreset, so there is no asset to point at. The reference is cleared rather
            // than left behind, where it would claim a preset that is not what is loaded.
            var current = string.IsNullOrWhiteSpace(presetName) ? this._currentPresetName : presetName;
            _currentPreset = null;
            _currentPresetName = $"{current} {GLOBAL_TAG}";
            return this;
        }

        /// <summary>
        /// Loads a preset from a JSON file. 
        /// The JSON file should represent a CsoundUnityPreset.
        /// <para>This doesn't set the preset. </para>
        /// You should use the Action to get the reference of the loaded preset.
        /// This is needed because of the async nature of this method, that uses a WebRequest on Android and iOS.
        /// <para>
        /// Example of usage:
        /// 
        /// <code>
        /// LoadPreset("myPath/presetName.json", (loadedPreset) => 
        /// {
        ///     SetPreset(loadedPreset);
        /// });
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="path">The path must point to an existing JSON file</param>
        /// <param name="onPresetLoaded">Called with the loaded preset, or with <c>null</c> when the file could not be read.</param>
        public void LoadPreset(string path, Action<CsoundUnityPreset> onPresetLoaded = null)
        {
            var presetName = Path.ChangeExtension(Path.GetFileName(path), null);

#if (UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        StartCoroutine(LoadingData(path, (d) =>
        {
            if (d == null)
            {
                onPresetLoaded?.Invoke(null);
                return;
            }
            var preset = CreatePreset(presetName, d);
            onPresetLoaded?.Invoke(preset);
        }));
#else
            if (!File.Exists(path))
            {
                Debug.LogError($"Preset JSON not found at path {path}");
                return;
            }
            var data = File.ReadAllText(path);
            var preset = CreatePreset(presetName, data);
            if (preset == null)
            {
                Debug.LogError($"Couldn't create preset from path: {path}");
                onPresetLoaded?.Invoke(null);
                return;
            }
            onPresetLoaded?.Invoke(preset);
#endif
        }

        /// <summary>
        /// Load a global preset on this CsoundUnity instance from a JSON file found at path. 
        /// The JSON file should represent a Global Preset, ie a complete CsoundUnity instance.
        /// <para>This also sets the global preset, overriding the current content of this CsoundUnity instance.</para>
        /// This could have unintended consequences in certain situations. 
        /// You could need to disable this CsoundUnity GameObject and enable it again to restore the Csound internal state.
        /// </summary>
        /// <param name="path">The path must point to an existing JSON file</param>
        public void LoadGlobalPreset(string path)
        {
            var presetName = Path.GetFileName(path);

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS)
        StartCoroutine(LoadingData(path, (d) =>
        {
            SetGlobalPreset(presetName, d);
        }));

#else
            if (!File.Exists(path))
            {
                Debug.LogError($"Global Preset JSON not found at path {path}");
                return;
            }
            var data = File.ReadAllText(path);
            SetGlobalPreset(presetName, data);
#endif
        }

        /// <summary>
        /// Parses a Cabbage <c>.snaps</c> file and returns a list of <see cref="CsoundUnityPreset"/> objects,
        /// one per snapshot entry, with channel metadata filled in from the companion CSD.
        /// </summary>
        /// <param name="csdPath">Full path to the CSD file used as the reference for channel types and ranges.</param>
        /// <param name="snapPath">Full path to the Cabbage <c>.snaps</c> file to parse.</param>
        /// <returns>A list of presets parsed from the snap file.</returns>
        public static List<CsoundUnityPreset> ParseSnap(string csdPath, string snapPath)
        {
            var snap = File.ReadAllText(snapPath);
            var snapStart = snap.IndexOf("{");
            var snapEnd = snap.LastIndexOf("}");
            var presets = snap.Substring(snapStart + 1, snapEnd - snapStart - 2);
            var csdName = Path.GetFileName(csdPath);
            var parsedPresets = ParsePresets(csdName, presets);
            var originalChannels = ParseCsdFile(csdPath);
            if (originalChannels == null || originalChannels.Count == 0)
            {
                Debug.LogWarning($"Couldn't fix preset channels for snap {snapPath}, csd path: {csdPath}, preset channels will not be visible on Editor, " +
                    $"but you should still be able to use them. Be aware that Comboboxes will be broken. " +
                    $"Please ensure that a '.csd' file with the same name of the '.snaps' file is present at the same location.");
                return parsedPresets;
            }
            foreach (var preset in parsedPresets)
            {
                FixPresetChannels(originalChannels, preset.channels);
            }
            return parsedPresets;
        }

        private static List<CsoundUnityPreset> ParsePresets(string snapName, string presets)
        {
            var parsedPresets = new List<CsoundUnityPreset>();
            var splitPresets = presets.Split(new string[] { "}," }, StringSplitOptions.None);

            foreach (var preset in splitPresets)
            {
                parsedPresets.Add(ParsePreset(snapName, preset));
            }
            return parsedPresets;
        }

        private static CsoundUnityPreset ParsePreset(string snapName, string preset)
        {
            var presetNameStart = preset.IndexOf("\"");
            var subPreset = preset.Substring(presetNameStart + 1, preset.Length - presetNameStart - 1);
            var presetNameEnd = subPreset.IndexOf("\"");
            var presetName = subPreset.Substring(0, presetNameEnd);
            var presetContentStart = preset.IndexOf("{");
            var presetContent = preset.Substring(presetContentStart + 1, preset.Length - presetContentStart - 1);
            var splitPresetContent = presetContent.Split(new string[] { "," }, StringSplitOptions.None);
            var presetChannels = new List<CsoundChannelController>();
            foreach (var chan in splitPresetContent)
            {
                presetChannels.Add(ParseChannel(chan).Clone());
            }

            return CreatePreset(presetName, snapName, presetChannels);
        }

        private static CsoundChannelController ParseChannel(string chan)
        {
            var split = chan.Split(new string[] { ":" }, StringSplitOptions.None);
            var chanName = split[0];
            var chanValue = split[1];
            var cleanChanName = chanName.Replace("\"", "").Trim();
            float.TryParse(chanValue, out float chanValueFloat);
            var cc = new CsoundChannelController()
            {
                channel = cleanChanName,
                value = chanValueFloat
            };
            return cc;
        }

        private static void FixPresetChannels(List<CsoundChannelController> originalChannels, List<CsoundChannelController> channelsToFix)
        {
            if (originalChannels == null || originalChannels.Count == 0)
            {
                Debug.LogError("Couldn't fix preset channels, aborting");
                return;
            }

            foreach (var chanToFix in channelsToFix)
            {
                foreach (var chan in originalChannels)
                {
                    if (chan.channel == chanToFix.channel)
                    {
                        // channel and value come from the Cabbage snap
                        // but not all the other fields
                        // set all the missing fields
                        chanToFix.caption   = chan.caption;
                        chanToFix.channelY  = chan.channelY;
                        chanToFix.increment = chan.increment;
                        chanToFix.max       = chan.max;
                        chanToFix.min       = chan.min;
                        chanToFix.minY      = chan.minY;
                        chanToFix.maxY      = chan.maxY;
                        chanToFix.options   = chan.options;
                        chanToFix.skew      = chan.skew;
                        chanToFix.text      = chan.text;
                        chanToFix.type      = chan.type;

                        // A Cabbage snap stores the channel as Csound holds it, so a combobox
                        // comes in 1-based and has to be brought back to the serialized index.
                        chanToFix.SetFromCsoundValue(chanToFix.value);
                    }
                }
            }
        }

        static IEnumerator LoadingData(string path, Action<string> onDataLoaded)
        {
            Debug.Log($"Loading data from path: {path}");
            using (var request = UnityWebRequest.Get(path))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.Log($"Couldn't load data at path: {path}: {request.error}");
                    onDataLoaded?.Invoke(null);
                    yield break;
                }
                var data = request.downloadHandler.text;
                onDataLoaded?.Invoke(data);
            }
        }

        private static string CheckPathForExistence(string path, string presetName, bool overwriteIfExisting)
        {
            path = string.IsNullOrWhiteSpace(path) ?
                Application.persistentDataPath
                : path;
            if (!char.IsSeparator(path[path.Length - 1]))
            {
                path = $"{path}/";
            }

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"Couldn't create folder at: {path}, defaulting to {Application.persistentDataPath} {ex.Message}");
                    path = Application.persistentDataPath;
                }
            }

            var fullPath = Path.Combine(path, presetName + ".json");

            var count = 0;
            while (File.Exists(fullPath) && !overwriteIfExisting)
            {
                fullPath = Path.Combine(path, $"{presetName}_{count++}.json");
            }
            return fullPath;
        }

        /// <summary>
        /// Controls how discrete channels (button, checkbox, combobox) are handled during <see cref="MorphToPreset"/>.
        /// </summary>
        public enum DiscreteChannelMode
        {
            /// <summary>Discrete channels are applied only at the very end, together with <see cref="SetPreset"/>. Default.</summary>
            SnapAtEnd,
            /// <summary>Discrete channels snap to the target value when the interpolation parameter crosses 0.5.</summary>
            SnapAtMidpoint,
            /// <summary>Discrete channels are set to the target value immediately when the morph begins.</summary>
            SnapAtStart,
        }

        /// <summary>
        /// Controls how discrete channels (button, checkbox, combobox) are handled during <see cref="BlendPresets"/>.
        /// </summary>
        public enum DiscreteBlendMode
        {
            /// <summary>Discrete channels are not modified during the blend. Default.</summary>
            Ignore,
            /// <summary>Discrete channels take the value from whichever corner currently has the highest weight.</summary>
            NearestCorner,
        }

        /// <summary>
        /// Smoothly interpolates all continuous channels — the sliders, plus both axes of an xypad,
        /// hrange or vrange — from their current values to the target preset's values over the
        /// given duration.
        /// An optional <see cref="AnimationCurve"/> controls easing; pass null for linear interpolation.
        /// The returned <see cref="Coroutine"/> can be passed to <see cref="StopMorph"/> to cancel mid-way.
        /// If duration is zero or negative, the preset is applied immediately.
        /// </summary>
        /// <param name="preset">The target preset to morph towards.</param>
        /// <param name="duration">Duration of the morph in seconds.  Zero or negative applies the preset instantly.</param>
        /// <param name="curve">Optional easing curve evaluated over [0,1]; <c>null</c> = linear.</param>
        /// <param name="onComplete">Optional callback invoked when the morph finishes.</param>
        /// <param name="discreteMode">Controls when button/checkbox/combobox channels are snapped to their target values.</param>
        /// <returns>The running <see cref="Coroutine"/>, or <c>null</c> when the preset was applied instantly.</returns>
        public Coroutine MorphToPreset(CsoundUnityPreset preset, float duration,
            AnimationCurve curve = null, Action onComplete = null,
            DiscreteChannelMode discreteMode = DiscreteChannelMode.SnapAtEnd)
        {
            if (duration <= 0f)
            {
                SetPreset(preset);
                onComplete?.Invoke();
                return null;
            }
            StopMorph();
            _morphCoroutine = StartCoroutine(MorphCoroutine(preset, duration, curve, onComplete, discreteMode));
            return _morphCoroutine;
        }

        /// <summary>
        /// Stops an in-progress morph started by <see cref="MorphToPreset"/>.
        /// Channel values remain at whatever point the morph reached when stopped.
        /// </summary>
        public void StopMorph()
        {
            if (_morphCoroutine == null) return;
            StopCoroutine(_morphCoroutine);
            _morphCoroutine = null;
        }

        private IEnumerator MorphCoroutine(CsoundUnityPreset preset, float duration,
            AnimationCurve curve, System.Action onComplete, DiscreteChannelMode discreteMode)
        {
            var startValues = new Dictionary<string, float>();
            foreach (var ch in channels)
            {
                if (!ch.IsContinuous || string.IsNullOrWhiteSpace(ch.channel)) continue;
                startValues[ch.channel] = ch.value;
                // One dictionary for both axes: X and Y are separate Csound channels with
                // distinct names, so they cannot collide.
                if (ch.HasSecondValue) startValues[ch.channelY] = ch.value2;
            }

            if (discreteMode == DiscreteChannelMode.SnapAtStart)
                ApplyDiscreteChannels(preset);

            var midpointSnapped = false;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                var rawT = Mathf.Clamp01(elapsed / duration);
                var t = curve?.Evaluate(rawT) ?? rawT;

                foreach (var target in preset.channels)
                {
                    if (!target.IsContinuous) continue;

                    MorphOneAxis(target, false, t,
                        MorphStartValue(startValues, preset, target.channel, target.value));

                    if (target.HasSecondValue)
                        MorphOneAxis(target, true, t,
                            MorphStartValue(startValues, preset, target.channelY, target.value2));
                }

                if (discreteMode == DiscreteChannelMode.SnapAtMidpoint && !midpointSnapped && rawT >= 0.5f)
                {
                    ApplyDiscreteChannels(preset);
                    midpointSnapped = true;
                }

                yield return null;
            }
            SetPreset(preset);
            _morphCoroutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Where the morph starts for one channel: its live value, or the target itself when this
        /// csd does not declare it.
        /// <para>
        /// No live channel by that name means no value to start from, so the morph has nothing to
        /// interpolate and the channel jumps straight to its target. That is the only sensible
        /// thing to do, but it is audible, so it is reported rather than left to sound like a
        /// broken interpolation.
        /// </para>
        /// </summary>
        private float MorphStartValue(Dictionary<string, float> startValues, CsoundUnityPreset preset,
            string channelName, float targetValue)
        {
            if (string.IsNullOrWhiteSpace(channelName)) return targetValue;
            if (startValues.TryGetValue(channelName, out var start)) return start;

            if (_warnedMissingPresetChannels.Add($"!live/{channelName}"))
                Debug.LogWarning($"[CsoundUnity] Preset \"{preset?.presetName}\" morphs " +
                                 $"channel \"{channelName}\", which this csd does not " +
                                 $"declare. With no starting value it jumps to the target " +
                                 $"instead of sliding to it. Reported once per channel.");

            return targetValue;
        }

        /// <summary>
        /// Moves one axis of a morphing channel to where it should be at <paramref name="t"/>.
        /// <para>
        /// Two-value widgets go through this twice, once per axis, because the ranges differ: see
        /// <see cref="UsesSkewedRangeY"/>.
        /// </para>
        /// </summary>
        private void MorphOneAxis(CsoundChannelController target, bool yAxis, float t, float start)
        {
            var channelName = yAxis ? target.channelY : target.channel;
            var end         = yAxis ? target.value2   : target.value;

            float min, max, skew;
            var skewed = yAxis
                ? UsesSkewedRangeY(target, out min, out max, out skew)
                : UsesSkewedRange(target, out min, out max, out skew);

            if (skewed)
            {
                // Sweep where the control is linear, so the morph moves at the rate the skew
                // describes instead of racing through one end of the range.
                var n = Mathf.Lerp(RU.RemapTo0to1(start, min, max, skew),
                                   RU.RemapTo0to1(end,   min, max, skew), t);
                SetChannel(channelName, RU.RemapFrom0to1(n, min, max, skew));
            }
            else
            {
                SetChannel(channelName, Mathf.Lerp(start, end, t));
            }
        }

        private void ApplyDiscreteChannels(CsoundUnityPreset preset)
        {
            foreach (var ch in preset.channels)
            {
                if (ch.type != "button" && ch.type != "checkbox" && !ch.IsCombobox) continue;
                SetChannel(ch.channel, ch.CsoundValue);
            }
        }

        /// <summary>
        /// Bilinear blend of four presets placed at the corners of a unit square.
        /// <para>Corner mapping: <paramref name="a"/> = (0,0), <paramref name="b"/> = (1,0),
        /// <paramref name="c"/> = (0,1), <paramref name="d"/> = (1,1).</para>
        /// <para>Continuous channels are interpolated bilinearly — the sliders, plus both axes of an
        /// xypad, hrange or vrange. Discrete channels (button, checkbox, combobox) follow
        /// <paramref name="discreteMode"/>.</para>
        /// Call this every frame (e.g. from <see cref="CsoundUnityVectorMorph"/>) to drive real-time vector synthesis.
        /// </summary>
        /// <param name="a">Preset at corner (0,0).</param>
        /// <param name="b">Preset at corner (1,0).</param>
        /// <param name="c">Preset at corner (0,1).</param>
        /// <param name="d">Preset at corner (1,1).</param>
        /// <param name="position">Normalised 2-D blend position, clamped to [0,1] on each axis.</param>
        /// <param name="discreteMode">How button/checkbox/combobox channels are resolved during the blend.</param>
        public void BlendPresets(CsoundUnityPreset a, CsoundUnityPreset b,
            CsoundUnityPreset c, CsoundUnityPreset d, Vector2 position,
            DiscreteBlendMode discreteMode = DiscreteBlendMode.Ignore)
        {
            var x = Mathf.Clamp01(position.x);
            var y = Mathf.Clamp01(position.y);
            var wA = (1 - x) * (1 - y);
            var wB = x       * (1 - y);
            var wC = (1 - x) * y;
            var wD = x       * y;

            WarnChannelsMissingFromFirstPreset(a, b, c, d);

            foreach (var chA in a.channels)
            {
                var isDiscrete = chA.type == "button" || chA.type == "checkbox" || chA.IsCombobox;

                if (isDiscrete && discreteMode == DiscreteBlendMode.NearestCorner)
                {
                    float[] weights   = { wA, wB, wC, wD };
                    CsoundUnityPreset[] presets = { a, b, c, d };
                    var maxIdx = 0;
                    for (var i = 1; i < 4; i++)
                        if (weights[i] > weights[maxIdx]) maxIdx = i;
                    var val = GetPresetChannelValue(presets[maxIdx], chA.channel, chA.value);
                    SetChannel(chA.channel, chA.ToCsoundValue(val));
                }
                else if (chA.IsContinuous)
                {
                    BlendOneAxis(chA, false, b, c, d, wA, wB, wC, wD);
                    if (chA.HasSecondValue)
                        BlendOneAxis(chA, true, b, c, d, wA, wB, wC, wD);
                }
            }
        }

        /// <summary>
        /// Whether this channel's values should be interpolated along its own curve rather than
        /// straight through the raw numbers, and the range to do it with.
        /// <para>
        /// A channel declared with a skew is not linear in its own units: on a frequency slider
        /// skewed towards the low end, the halfway point of the control is nowhere near the
        /// arithmetic mean of its endpoints. Interpolating the raw values therefore sweeps at the
        /// wrong rate and lands off-centre — which is what the skew existed to prevent.
        /// </para>
        /// Returns <c>false</c> for channels with no usable range or no skew, where the raw
        /// values are already the right thing to interpolate.
        /// <para>
        /// The range is taken from the live channel rather than from the preset, which only holds
        /// a copy made when it was saved: adding a skew to the csd would otherwise have no effect
        /// until every preset was re-saved. The preset's own copy is the fallback for channels the
        /// current csd no longer declares.
        /// </para>
        /// </summary>
        private bool UsesSkewedRange(CsoundChannelController presetChannel, out float min, out float max, out float skew)
        {
            var channel = (presetChannel != null ? GetChannelController(presetChannel.channel) : null) ?? presetChannel;

            min  = channel?.min  ?? 0f;
            max  = channel?.max  ?? 0f;
            skew = channel?.skew ?? 1f;
            return channel != null && max > min && skew > 0f && skew != 1f;
        }

        /// <summary>
        /// The same question as <see cref="UsesSkewedRange"/>, asked of a two-value widget's
        /// second axis.
        /// <para>
        /// An xypad declares its own Y range, <c>minY</c>..<c>maxY</c>, and declares no skew for
        /// it, so the Y axis is linear. An hrange or vrange puts both handles on one axis, so its
        /// second value shares the first one's range — and its skew, which is why this defers
        /// rather than assuming linear.
        /// </para>
        /// </summary>
        private bool UsesSkewedRangeY(CsoundChannelController presetChannel, out float min, out float max, out float skew)
        {
            var channel = (presetChannel != null ? GetChannelController(presetChannel.channel) : null) ?? presetChannel;

            if (channel == null || channel.type != "xypad")
                return UsesSkewedRange(presetChannel, out min, out max, out skew);

            min  = channel.minY;
            max  = channel.maxY;
            skew = 1f;
            return false;
        }

        /// <summary>
        /// Blends one axis of a channel across the four corners and writes it.
        /// <para>
        /// Two-value widgets go through this twice, once per axis. The corner lookups use the
        /// widget's own channel name both times, because a preset keeps both values on the one
        /// controller.
        /// </para>
        /// </summary>
        private void BlendOneAxis(CsoundChannelController chA, bool yAxis,
            CsoundUnityPreset b, CsoundUnityPreset c, CsoundUnityPreset d,
            float wA, float wB, float wC, float wD)
        {
            var channelName = yAxis ? chA.channelY : chA.channel;
            var vA = yAxis ? chA.value2 : chA.value;
            var vB = GetPresetChannelValue(b, chA.channel, vA, yAxis);
            var vC = GetPresetChannelValue(c, chA.channel, vA, yAxis);
            var vD = GetPresetChannelValue(d, chA.channel, vA, yAxis);

            float min, max, skew;
            var skewed = yAxis
                ? UsesSkewedRangeY(chA, out min, out max, out skew)
                : UsesSkewedRange(chA, out min, out max, out skew);

            if (skewed)
            {
                // Blend where the control is linear, then map back into channel units.
                var n = RU.RemapTo0to1(vA, min, max, skew) * wA
                      + RU.RemapTo0to1(vB, min, max, skew) * wB
                      + RU.RemapTo0to1(vC, min, max, skew) * wC
                      + RU.RemapTo0to1(vD, min, max, skew) * wD;
                SetChannel(channelName, RU.RemapFrom0to1(n, min, max, skew));
            }
            else
            {
                SetChannel(channelName, vA * wA + vB * wB + vC * wC + vD * wD);
            }
        }


        /// <summary>Last four presets scanned, so the scan below runs on change and not per frame.</summary>
        private CsoundUnityPreset _lastBlendA, _lastBlendB, _lastBlendC, _lastBlendD;

        /// <summary>
        /// Reports channels that the other three presets declare but the first one does not.
        /// <para>
        /// <see cref="BlendPresets"/> walks the first preset's channels, so anything missing from
        /// it is not blended at all — not blended wrongly, simply absent, which is why it is easy
        /// to miss. The behaviour is left alone: what the first corner should contribute for a
        /// channel it has no opinion about is a design question, not something to settle inside a
        /// warning.
        /// </para>
        /// <para>
        /// The scan is O(channels) and BlendPresets runs every frame, so it only re-runs when the
        /// set of four presets actually changes.
        /// </para>
        /// </summary>
        private void WarnChannelsMissingFromFirstPreset(CsoundUnityPreset a, CsoundUnityPreset b,
            CsoundUnityPreset c, CsoundUnityPreset d)
        {
            if (ReferenceEquals(a, _lastBlendA) && ReferenceEquals(b, _lastBlendB) &&
                ReferenceEquals(c, _lastBlendC) && ReferenceEquals(d, _lastBlendD)) return;

            _lastBlendA = a; _lastBlendB = b; _lastBlendC = c; _lastBlendD = d;

            if (a?.channels == null) return;

            foreach (var other in new[] { b, c, d })
            {
                if (other?.channels == null) continue;

                foreach (var ch in other.channels)
                {
                    if (string.IsNullOrWhiteSpace(ch.channel)) continue;
                    if (a.channels.Exists(x => x.channel == ch.channel)) continue;

                    // Keyed by the first preset and the channel, not by which of the other three
                    // happens to declare it: the problem is one — A is missing it — and all
                    // three would otherwise report the same thing. Swapping A in play mode still
                    // re-warns, which is the case the rescan exists for.
                    if (!_warnedMissingPresetChannels.Add($"!first/{a.presetName}/{ch.channel}"))
                        continue;

                    Debug.LogWarning($"[CsoundUnity] Preset \"{other.presetName}\" declares channel " +
                                     $"\"{ch.channel}\", but the first preset of the blend " +
                                     $"(\"{a.presetName}\") does not, so that channel takes no part in " +
                                     $"the blend at all. Reported once per first preset and channel.");
                }
            }
        }

        /// <summary>
        /// Channels already reported as missing from a preset. A vector morph calls
        /// <see cref="BlendPresets"/> every Update, so the warning has to fire once and then
        /// keep quiet. Cleared by <c>Init</c>, so a new csd gets a fresh hearing.
        /// </summary>
        private readonly HashSet<string> _warnedMissingPresetChannels = new HashSet<string>();

        /// <summary>
        /// The preset's value for the channel, or <paramref name="fallback"/> when the preset does
        /// not have that channel — which happens when the presets were saved against a different
        /// version of the csd. Reported, because it does not look like a bug: the blend still runs,
        /// that corner of it is just anchored to the wrong value.
        /// </summary>
        /// <param name="preset">The corner preset to read from.</param>
        /// <param name="channel">The channel name, which is the widget's own name on both axes.</param>
        /// <param name="fallback">Returned when the preset has no channel by that name.</param>
        /// <param name="secondValue">Read the Y value of a two-value widget instead of the X one.</param>
        private float GetPresetChannelValue(CsoundUnityPreset preset, string channel, float fallback,
            bool secondValue = false)
        {
            if (preset?.channels != null)
                foreach (var ch in preset.channels)
                    if (ch.channel == channel) return secondValue ? ch.value2 : ch.value;

            if (_warnedMissingPresetChannels.Add($"{preset?.presetName}/{channel}"))
                Debug.LogWarning($"[CsoundUnity] Preset \"{preset?.presetName}\" has no channel " +
                                 $"\"{channel}\", so it contributes the first preset's value to the " +
                                 $"blend instead of its own. The presets were most likely saved " +
                                 $"against a different version of the csd. Reported once per " +
                                 $"channel and preset.");

            return fallback;
        }

        #endregion PRESETS

        #region CLIPBOARD

        /// <summary>
        /// Whether the clipboard can carry and restore this channel. It needs a name — the csd's
        /// <c>form</c>, and any widget without one, is in the list but is not a channel — and a
        /// button is excluded because a momentary trigger has no value worth putting back.
        /// <para>
        /// Copy and paste share this so the two report the same number, instead of one counting
        /// controllers and the other counting what it managed to apply.
        /// </para>
        /// </summary>
        private static bool IsRestorable(CsoundChannelController ch) =>
            ch != null && !string.IsNullOrWhiteSpace(ch.channel) &&
            (ch.type == null || !ch.type.Contains("button"));

        /// <summary>
        /// Puts this instance's control channels on the system clipboard, as the same JSON a
        /// <see cref="CsoundUnityPreset"/> is saved in — so a clipboard snapshot and a saved
        /// <c>.json</c> preset are interchangeable, and channels can be pasted into a chat window
        /// as readily as back into an inspector.
        /// <para>
        /// For the quick pass a preset asset is too heavy for: try a change, dislike it, paste the
        /// old values back. Works in play mode.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> when something was copied.</returns>
        public bool CopyChannelsToClipboard()
        {
            if (channels == null || channels.Count == 0)
            {
                Debug.LogWarning("[CsoundUnity] Nothing to copy: this instance has no control channels.");
                return false;
            }

            // The whole list goes on the clipboard, form and all, so it stays interchangeable
            // with a saved .json preset. Only the restorable ones are worth counting out loud.
            GUIUtility.systemCopyBuffer =
                JsonUtility.ToJson(CreatePreset($"{name} channels", csoundFileName, channels), true);

            Debug.Log($"[CsoundUnity] Copied {channels.FindAll(IsRestorable).Count} "
                    + $"channel(s) to the clipboard.");
            return true;
        }

        /// <summary>
        /// Applies channels held on the clipboard in the <see cref="CsoundUnityPreset"/> JSON format.
        /// <para>
        /// Unlike <see cref="SetPreset(CsoundUnityPreset)"/> this does not refuse when the clipboard
        /// came from another csd. Refusing would defeat the purpose — the clipboard is for moving
        /// values by hand, between instances and between csds that share channel names — so the
        /// channels this csd declares are applied and the rest are reported.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> when at least one channel was applied.</returns>
        public bool PasteChannelsFromClipboard()
        {
            var json = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[CsoundUnity] Nothing to paste: the clipboard is empty.");
                return false;
            }

            var preset = CreatePreset(string.Empty, json);

            // CreatePreset only complains about malformed json. Valid json that is not a preset
            // deserialises into an empty one, which would otherwise apply nothing in silence.
            if (preset == null || preset.channels == null || preset.channels.Count == 0)
            {
                Debug.LogError("[CsoundUnity] The clipboard does not hold CsoundUnity channels. " +
                               "Expected the JSON a preset is saved in.");
                return false;
            }

            var unknown = new List<string>();
            var applied = 0;

            foreach (var ch in preset.channels)
            {
                if (!IsRestorable(ch)) continue;

                // GetChannelController resolves against the live csd, and repairs the name index
                // if a domain reload emptied it.
                if (GetChannelController(ch.channel) == null) { unknown.Add(ch.channel); continue; }

                SetChannel(ch);
                applied++;
            }

            var from = preset.csoundFileName != csoundFileName && !string.IsNullOrEmpty(preset.csoundFileName)
                ? $" (copied from \"{preset.csoundFileName}\")" : string.Empty;
            var skipped = unknown.Count > 0
                ? $" {unknown.Count} not in this csd: {string.Join(", ", unknown)}." : string.Empty;

            if (applied == 0)
                Debug.LogError($"[CsoundUnity] Nothing pasted{from}.{skipped}");
            else if (unknown.Count > 0)
                Debug.LogWarning($"[CsoundUnity] Pasted {applied} channel(s){from}.{skipped}");
            else
                Debug.Log($"[CsoundUnity] Pasted {applied} channel(s){from}.");

            // Same case as applying a preset: channels written from outside the UI, so the
            // components have to re-read. This has its own loop and does not go through
            // SetChannels, hence the second call site.
            if (applied > 0) OnChannelsUpdated?.Invoke();

            return applied > 0;
        }

        #endregion CLIPBOARD
    }
}
