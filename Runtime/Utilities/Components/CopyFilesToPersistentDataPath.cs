/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using WAFU = Csound.Unity.Utilities.WriteAudioFileUtils;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Copies files from <c>Resources</c> or <c>StreamingAssets</c> into
    /// <c>Application.persistentDataPath</c> so that Csound can find them via
    /// Environment Variables (e.g. <c>SFDIR</c>, <c>SSDIR</c>, <c>SADIR</c>).
    ///
    /// <para>
    /// Supported file types:
    /// <list type="bullet">
    ///   <item><description><b>Audio files</b> — any format supported by Csound (wav, aif, …). Place them in a <c>Resources</c> folder as <c>AudioClip</c> assets.</description></item>
    ///   <item><description><b>Sound fonts</b> — sf2 files. Rename the extension to <c>.bytes</c> or <c>.txt</c> so Unity imports them as <c>TextAsset</c>, then place them in <c>Resources</c>.</description></item>
    ///   <item><description><b>Csound plugins</b> — desktop only (dylib / dll). Rename to <c>.bytes</c>, place in <c>Resources</c>. On Android / iOS there is no way to load plugins from a runtime path, so they must be bundled at build time instead.</description></item>
    ///   <item><description><b>Additional binary files</b> — any file importable as <c>TextAsset</c> (<c>.bytes</c> / <c>.txt</c> extension). Useful for wavetables, IRs, or any other data Csound needs to read from disk.</description></item>
    ///   <item><description><b>StreamingAssets files</b> — files placed in the <c>StreamingAssets</c> folder. On Android these are read via <c>UnityWebRequest</c>; on other platforms they are copied directly.</description></item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Setup:</b> keep the CsoundUnity GameObjects <b>inactive</b> when entering Play Mode.
    /// This component copies the files first, then activates the listed CsoundUnity instances,
    /// ensuring the Environment Variables point to valid files before Csound starts.
    /// Set <c>autoStart</c> to <c>false</c> if you need to trigger the copy manually via <see cref="Copy"/>.
    /// It could be extended to support CsoundUnity prefabs too, but for now activation of existing instances is left to the user.
    /// </para>
    ///
    /// <para>
    /// See the <c>Environment/SFDIR</c> and <c>Environment/Load Plugins</c> samples for
    /// complete working examples.
    /// </para>
    /// </summary>
    public class CopyFilesToPersistentDataPath : MonoBehaviour
    {
        #region Serialized fields

        [Tooltip("Those audio files will be searched into the Resources folder, and copied into the Persistent Data path. " +
            "Please specify the destination extension, only wav or aif are supported")]
        [SerializeField] private AudioFileInfo[] _audioFiles;
        [Tooltip("The names of the plugins to copy from Resources to the Persistent Data Path folder. " +
            "Don't specify the extension. The extension will be added to the copied files depending on the platform. ")]
        [SerializeField] private string[] _pluginsNames;
        [Tooltip("Those files will be read from the StreamingAssets folder. Please specify also the extension of the file.")]
        [SerializeField] private string[] _streamingAssetsFiles;
        [Tooltip("Binary files to copy from Resources to persistentDataPath. " +
            "Use this for any file Csound reads from disk that is not an AudioClip or a plugin: " +
            "e.g. soundfonts (.sf2), wavetables, impulse responses, MIDI files, or custom data files.\n\n" +
            "In Unity's Resources folder the file must have a .bytes or .txt extension so it is imported as a TextAsset " +
            "(see https://docs.unity3d.com/Manual/class-TextAsset.html). " +
            "Specify the actual destination extension in the Extension field — " +
            "the file will be copied with that extension so Csound can find it.")]
        [SerializeField] private AdditionalFileInfo[] _additionalFiles;
        [Tooltip("Ensure these CsoundUnity GameObjects are inactive when hitting play, " +
            "otherwise their initialization will run. " +
            "Setting the Environment Variables on a running Csound instance can have unintended effects.")]
        [SerializeField] private CsoundUnity[] _csoundUnitys;
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private bool _fallbackToWav = true;

        #endregion Serialized fields

        #region Fields

        public bool copyCompleted = false;

        private int _filesToCopy;
        private int _copiedFiles;

        #endregion Fields

        #region Unity messages

        void Awake()
        {
            if (_autoStart)
            {
                Copy();
            }
        }

        #endregion Unity messages

        #region Public API

        /// <summary>
        /// Set the AudioFiles to load before calling Copy. Intended usage of this function is when autoStart is false
        /// </summary>
        /// <param name="audioClips"></param>
        /// <param name="directory"></param>
        public void SetAudioFiles(AudioClip[] audioClips, string directory = "", string extension = "wav")
        {
            var audioFiles = new AudioFileInfo[audioClips.Length];
            for (var i = 0; i < audioFiles.Length; i++)
            {
                audioFiles[i] = new AudioFileInfo() { Directory = directory, FileName = audioClips[i].name + "." + extension };
            }
            _audioFiles = audioFiles;
        }

        /// <summary>
        /// Start the copy process
        /// </summary>
        public void Copy()
        {
            copyCompleted = false;

            // Null-guard: with Domain Reload disabled, serialized arrays may not be
            // re-initialized before the first Awake(), leaving them null instead of empty.
            _audioFiles ??= Array.Empty<AudioFileInfo>();
            _pluginsNames ??= Array.Empty<string>();
            _streamingAssetsFiles ??= Array.Empty<string>();
            _additionalFiles ??= Array.Empty<AdditionalFileInfo>();

#if UNITY_ANDROID || UNITY_IOS
            _filesToCopy = _audioFiles.Length + _streamingAssetsFiles.Length + _additionalFiles.Length;
#else
            _filesToCopy = _audioFiles.Length + _pluginsNames.Length + _streamingAssetsFiles.Length + _additionalFiles.Length;
#endif

            Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Copying / Checking {_filesToCopy} files to Persistent Data Path");

            StartCoroutine(CopyAudioFiles());

#if !UNITY_ANDROID && !UNITY_IOS
            foreach (var pluginName in _pluginsNames)
            {
                Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Copying plugin: {pluginName}");
                var dir = Application.persistentDataPath;
                var pluginPath = string.Empty;
                var destinationPath = string.Empty;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                destinationPath = Path.Combine(dir, pluginName + ".dll");
                pluginPath = Path.Combine("Win", pluginName);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                destinationPath = Path.Combine(dir, "lib" + pluginName + ".dylib");
                pluginPath = Path.Combine("MacOS", "lib" + pluginName);
#endif
                Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: File Exists? {File.Exists(destinationPath)}");
                if (!File.Exists(destinationPath))
                {
                    Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Loading plugin at path: {pluginPath}");
                    var plugin = Resources.Load<TextAsset>(pluginPath);
                    Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Loaded plugin bytes: {plugin.bytes.Length}");
                    Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Writing plugin file at path: {destinationPath}");
                    WriteFile(plugin.bytes, destinationPath);
                }
                _copiedFiles++;
            }
#endif

            foreach (var streamingAssetFile in _streamingAssetsFiles)
            {
                var destinationPath = Path.Combine(Application.persistentDataPath, streamingAssetFile);
                CopyFileFromStreamingAssets(streamingAssetFile, destinationPath);
            }

            foreach (var additionalFile in _additionalFiles)
            {
                var dir = Path.Combine(Application.persistentDataPath, additionalFile.Directory);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var filePath = Path.Combine(additionalFile.Directory, additionalFile.FileName + "." + additionalFile.Extension);
                var destinationPath = Path.Combine(dir, additionalFile.FileName + "." + additionalFile.Extension);

                Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Copying additional File from Resources: {additionalFile.FileName}, destinationPath: {destinationPath}, file Exists? {File.Exists(destinationPath)}");
                if (!File.Exists(destinationPath))
                {
                    CopyGenericFileFromResources(filePath, destinationPath);
                }
                _copiedFiles++;
            }

            StartCoroutine(WaitForCopy());
        }

        #endregion Public API

        #region Private helpers

        IEnumerator CopyAudioFiles()
        {
            foreach (var audioFile in _audioFiles)
            {
                var dir = string.IsNullOrWhiteSpace(audioFile.Directory) ?
                    Application.persistentDataPath :
                    Application.persistentDataPath + "/" + audioFile.Directory;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var filePath = string.IsNullOrWhiteSpace(audioFile.Directory) ? audioFile.FileName : audioFile.Directory + "/" + audioFile.FileName;
                var destinationPath = dir + "/" + audioFile.FileName;

                if (!File.Exists(destinationPath))
                {
                    Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Copying audio file from Resources: {audioFile.FileName}, dir {audioFile.Directory}, destinationPath: {destinationPath}, file Exists? {File.Exists(destinationPath)}");
                    CopyAudioFileFromResources(filePath, destinationPath);
                    // wait one frame between each copy to avoid locking the main thread for too long
                    yield return null;
                }

                _copiedFiles++;
            }
        }

        IEnumerator WaitForCopy()
        {
            while (_copiedFiles < _filesToCopy)
            {
                yield return null;
            }

            copyCompleted = true;

            foreach (var csound in _csoundUnitys)
            {
                csound.gameObject.SetActive(true);
            }
        }

        private void CopyFileFromStreamingAssets(string origin, string destination)
        {
            var path = Path.Combine(Application.streamingAssetsPath, origin);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (!File.Exists(destination))
            {
                var bytes = File.ReadAllBytes(path);
                WriteFile(bytes, destination);
            }
            _copiedFiles++;
#else
            if (!File.Exists(destination))
            {
                StartCoroutine(GetRequest(path, (bytes) =>
                {
                    WriteFile(bytes, destination);
                    _copiedFiles++;
                }));
            }
            else
            {
                _copiedFiles++;
            }
#endif
        }

        IEnumerator GetRequest(string uri, Action<byte[]> onBytesLoaded)
        {
            using (var req = UnityWebRequest.Get(uri))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                switch (req.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError($"Csound.Unity.LoadFiles Error: {req.error}");
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError($"Csound.Unity.LoadFiles HTTP Error: {req.error}");
                        break;
                    case UnityWebRequest.Result.Success:
                        Debug.Log($"Csound.Unity.LoadFiles: {req.downloadHandler.data.Length} bytes read");
                        onBytesLoaded?.Invoke(req.downloadHandler.data);
                        break;
                }
#else
                if (req.result == UnityWebRequest.Result.ConnectionError ||
                    req.result == UnityWebRequest.Result.ProtocolError ||
                    req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError($"Csound.Unity.CopyFilesToPersistentDataPath Error: {req.error}");
                    yield break;
                }
                Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: {req.downloadHandler.data.Length} bytes read");
                onBytesLoaded?.Invoke(req.downloadHandler.data);
#endif
            }
        }

        private void CopyAudioFileFromResources(string origin, string destination)
        {
            var pathWithoutExtension = Path.ChangeExtension(origin, null);
            var audioClip = Resources.Load<AudioClip>(pathWithoutExtension);
            if (!audioClip)
            {
                Debug.LogError($"Csound.Unity.CopyFilesToPersistentDataPath Error: AudioClip at {origin} couldn't be loaded.");
                return;
            }

            var data = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(data, 0);

            WAFU.WriteAudioFile(audioClip, destination, 16, _fallbackToWav);
        }

        private static void CopyGenericFileFromResources(string origin, string destination)
        {
            var pathWithoutExtension = Path.ChangeExtension(origin, null);
            var textAsset = Resources.Load<TextAsset>(pathWithoutExtension);
            WriteFile(textAsset.bytes, destination);
        }

        private static void WriteFile(byte[] bytes, string destination)
        {
            Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Writing file ({bytes.Length} bytes) at path: {destination}");
            var s = new MemoryStream(bytes);
            var br = new BinaryReader(s);
            var dir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var bw = new BinaryWriter(File.Open(destination, FileMode.OpenOrCreate));
            bw.Write(br.ReadBytes(bytes.Length));
        }

        #endregion Private helpers

        #region Nested types

        [Serializable]
        public class AdditionalFileInfo
        {
            [Tooltip("The file name without extension")]
            public string FileName;
            [Tooltip("The extension of the copied file, without the dot")]
            public string Extension;
            [Tooltip("The directory where the file is contained / will be placed after copy")]
            public string Directory;
        }

        [Serializable]
        public class AudioFileInfo
        {
            [Tooltip("The file name with extension")]
            public string FileName;
            [Tooltip("The directory where the file is contained / will be placed after copy")]
            public string Directory;
        }

        #endregion Nested types
    }
}
