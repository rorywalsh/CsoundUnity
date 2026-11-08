/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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

        /// <summary>True when every file was copied or already existed. False after failure or cancellation.</summary>
        public bool copyCompleted = false;

        /// <summary>True while a copy operation is running. Further Copy calls are ignored until it ends.</summary>
        public bool IsCopying { get; private set; }

        /// <summary>Number of files that failed in the latest copy operation.</summary>
        public int FailedFileCount { get; private set; }

        private Coroutine _copyRoutine;
        private UnityWebRequest _copyRequest;

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
        /// <param name="audioClips">The clips to copy. Each file is named after its clip.</param>
        /// <param name="directory">Subfolder under the persistent data path. Empty copies to the root.</param>
        /// <param name="extension">Extension appended to each clip name, without the dot.</param>
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
        /// Start copying files. An overlapping call is ignored; a later call starts a fresh operation.
        /// Csound instances are activated only if every file succeeds. Inspect IsCopying and FailedFileCount on failure.
        /// </summary>
        public void Copy()
        {
            if (IsCopying) return;
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("Csound.Unity.CopyFilesToPersistentDataPath: activate this GameObject before copying.");
                return;
            }

            copyCompleted = false;
            FailedFileCount = 0;
            _audioFiles ??= Array.Empty<AudioFileInfo>();
            _pluginsNames ??= Array.Empty<string>();
            _streamingAssetsFiles ??= Array.Empty<string>();
            _additionalFiles ??= Array.Empty<AdditionalFileInfo>();
            _csoundUnitys ??= Array.Empty<CsoundUnity>();
            IsCopying = true;
            _copyRoutine = StartCoroutine(CopyFiles());
        }

        #endregion Public API

        #region Private helpers

        private void OnDisable()
        {
            if (!IsCopying) return;
            if (_copyRoutine != null) StopCoroutine(_copyRoutine);
            _copyRequest?.Dispose();
            _copyRequest = null;
            _copyRoutine = null;
            IsCopying = false;
            copyCompleted = false;
        }

        private IEnumerator CopyFiles()
        {
            try
            {
                foreach (var file in _audioFiles)
                {
                    TryCopy(() =>
                    {
                        var path = Path.Combine(file.Directory ?? "", file.FileName);
                        var destination = Path.Combine(Application.persistentDataPath, path);
                        if (File.Exists(destination)) return;
                        var clip = Resources.Load<AudioClip>(Path.ChangeExtension(path, null));
                        if (clip == null) throw new FileNotFoundException("AudioClip not found in Resources.", path);
                        WriteCopiedFile(destination, temporary =>
                        {
                            if (!WAFU.WriteAudioFile(clip, temporary, 16, _fallbackToWav))
                                throw new IOException($"Could not write audio file: {destination}");
                        });
                    }, file?.FileName);
                    yield return null;
                }

#if !UNITY_ANDROID && !UNITY_IOS
                foreach (var plugin in _pluginsNames)
                {
                    TryCopy(() =>
                    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                        var path = Path.Combine("Win", plugin);
                        var destination = Path.Combine(Application.persistentDataPath, plugin + ".dll");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                        var path = Path.Combine("MacOS", "lib" + plugin);
                        var destination = Path.Combine(Application.persistentDataPath, "lib" + plugin + ".dylib");
#else
                        throw new PlatformNotSupportedException("Runtime plugin copying requires desktop Windows or macOS.");
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                        if (!File.Exists(destination)) CopyResource(path, destination);
#endif
                    }, plugin);
                    yield return null;
                }
#endif

                foreach (var file in _streamingAssetsFiles)
                    yield return CopyStreamingAsset(file);

                foreach (var file in _additionalFiles)
                {
                    TryCopy(() =>
                    {
                        var path = Path.Combine(file.Directory ?? "", file.FileName + "." + file.Extension);
                        var destination = Path.Combine(Application.persistentDataPath, path);
                        if (!File.Exists(destination)) CopyResource(Path.ChangeExtension(path, null), destination);
                    }, file?.FileName);
                    yield return null;
                }

                copyCompleted = FailedFileCount == 0;
                if (copyCompleted)
                    foreach (var csound in _csoundUnitys)
                        if (csound != null) csound.gameObject.SetActive(true);
            }
            finally
            {
                IsCopying = false;
                _copyRoutine = null;
            }
        }

        private IEnumerator CopyStreamingAsset(string file)
        {
            string path = null, destination = null;
            if (!TryCopy(() =>
            {
                path = Path.Combine(Application.streamingAssetsPath, file);
                destination = Path.Combine(Application.persistentDataPath, file);
            }, file)) yield break;
            if (File.Exists(destination)) yield break;

            if (!path.Contains("://"))
            {
                TryCopy(() => WriteFile(File.ReadAllBytes(path), destination), file);
                yield break;
            }

            yield return DownloadFile(path, destination);
        }

        private IEnumerator DownloadFile(string path, string destination)
        {
            UnityWebRequest request = null;
            UnityWebRequestAsyncOperation operation = null;
            if (!TryCopy(() =>
            {
                request = UnityWebRequest.Get(path.Replace('\\', '/'));
                _copyRequest = request;
                request.timeout = 30;
                operation = request.SendWebRequest();
            }, path))
            {
                request?.Dispose();
                _copyRequest = null;
                yield break;
            }
            try
            {
                yield return operation;
                if (request.result != UnityWebRequest.Result.Success)
                    ReportFailure(path, request.error);
                else
                    TryCopy(() => WriteFile(request.downloadHandler.data, destination), path);
            }
            finally
            {
                if (_copyRequest == request)
                {
                    _copyRequest = null;
                    request.Dispose();
                }
            }
        }

        private bool TryCopy(Action copy, string file)
        {
            try
            {
                copy();
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure(file, exception.Message);
                return false;
            }
        }

        private void ReportFailure(string file, string error)
        {
            FailedFileCount++;
            Debug.LogError($"Csound.Unity.CopyFilesToPersistentDataPath: '{file}': {error}");
        }

        private static void CopyResource(string path, string destination)
        {
            var resource = Resources.Load<TextAsset>(path);
            if (resource == null) throw new FileNotFoundException("TextAsset not found in Resources.", path);
            WriteFile(resource.bytes, destination);
        }

        private static void WriteFile(byte[] bytes, string destination)
        {
            WriteCopiedFile(destination, temporary => File.WriteAllBytes(temporary, bytes));
        }

        private static void WriteCopiedFile(string destination, Action<string> write)
        {
            var directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + Path.GetExtension(destination));
            try
            {
                write(temporary);
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        #endregion Private helpers

        #region Nested types

        /// <summary>One non-audio file to copy, such as a csd or a soundfont.</summary>
        [Serializable]
        public class AdditionalFileInfo
        {
            /// <summary>The file name, without the extension.</summary>
            [Tooltip("The file name without extension")]
            public string FileName;

            /// <summary>The extension, without the dot.</summary>
            [Tooltip("The extension of the copied file, without the dot")]
            public string Extension;

            /// <summary>The folder the file sits in, and the one it is copied into.</summary>
            [Tooltip("The directory where the file is contained / will be placed after copy")]
            public string Directory;
        }

        /// <summary>One audio file to copy.</summary>
        [Serializable]
        public class AudioFileInfo
        {
            /// <summary>The file name, extension included.</summary>
            [Tooltip("The file name with extension")]
            public string FileName;

            /// <summary>The folder the file sits in, and the one it is copied into.</summary>
            [Tooltip("The directory where the file is contained / will be placed after copy")]
            public string Directory;
        }

        #endregion Nested types
    }
}
