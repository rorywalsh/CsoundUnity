using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using WAFU = Csound.Unity.Utilities.WriteAudioFileUtils;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Utility class that copies different kind of files into the Persistent Data Path so that they can be found by Csound using Environment Variables.
    /// <para>For this to work the CsoundUnity GameObjects have to be disabled when entering PlayMode.
    /// When the copy is done it will enable all the CsoundUnity instances set.</para>
    /// It could be extended to use CsoundUnity prefabs too (see Samples/EnvironmentVars/SFDIR), but for now this is left to the user.
    /// Note: Plugins are not copied for Android and iOS since there's no way of loading them from a path at runtime, unlike desktop platforms
    /// </summary>
    public class CopyFilesToPersistentDataPath : MonoBehaviour
    {
        [Tooltip("Those audio files will be searched into the Resources folder, and copied into the Persistent Data path")]
        [SerializeField] private AudioFileInfo[] _audioFiles;
        [Tooltip("The names of the plugins to copy from Resources to the Persistent Data Path folder. " +
            "Don't specify the extension. The extension will be added to the copied files depending on the platform. ")]
        [SerializeField] private string[] _pluginsNames;
        [Tooltip("Those files will be read from the StreamingAssets folder. Please specify also the extension of the file.")]
        [SerializeField] private string[] _streamingAssetsFiles;
        [Tooltip("Those files will be read from Resources folders. Only specify the file name, no need to specify the extension too." +
            "Be sure though to rename these additional files extensions to .txt or .bytes. " +
            "See https://docs.unity3d.com/Manual/class-TextAsset.html")]
        [SerializeField] private AdditionalFileInfo[] _additionalFiles;
        [Tooltip("Ensure these CsoundUnity GameObjects are inactive when hitting play, " +
            "otherwise their initialization will run. " +
            "Setting the Environment Variables on a running Csound instance can have unintended effects.")]
        [SerializeField] private CsoundUnity[] _csoundUnitys;
        [SerializeField] private bool _autoStart = true;

        public bool copyCompleted = false;

        private int _filesToCopy;
        private int _copiedFiles;

        void Awake()
        {
            if (_autoStart)
            {
                Copy();
            }
        }

        /// <summary>
        /// Set the AudioFiles to load before calling Copy. Intended usage of this function is when autoStart is false
        /// </summary>
        /// <param name="audioClips"></param>
        /// <param name="directory"></param>
        public void SetAudioFiles(AudioClip[] audioClips, string directory = "")
        {
            var audioFiles = new AudioFileInfo[audioClips.Length];
            for (var i = 0; i < audioFiles.Length; i++)
            {
                audioFiles[i] = new AudioFileInfo() { Directory = directory, FileName = audioClips[i].name };
            }
            _audioFiles = audioFiles;
        }

        /// <summary>
        /// Start the copy process 
        /// </summary>
        public void Copy()
        {
            copyCompleted = false;
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

            // start waiting for all the files to be copied, at the end enable all the CsoundUnity instances
            StartCoroutine(WaitForCopy());
        }

        IEnumerator CopyAudioFiles()
        {
            foreach (var audioFile in _audioFiles)
            {
                var dir = string.IsNullOrWhiteSpace(audioFile.Directory) ?
                    Application.persistentDataPath :
                    Application.persistentDataPath + "/" + audioFile.Directory;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var filePath = audioFile.Directory + "/" + audioFile.FileName;
                var destinationPath = dir + "/" + audioFile.FileName;

                if (!File.Exists(destinationPath))
                {
                    Debug.Log($"Csound.Unity.CopyFilesToPersistentDataPath: Copying audio file from Resources: {audioFile.FileName}, dir {audioFile.Directory}, destinationPath: {destinationPath}, file Exists? {File.Exists(destinationPath)}");
                    CopyAudioFileFromResources(filePath, destinationPath);
                    // wait one frame between each copy to avoid locking too much the main thread
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

            // finally enable all the Csound instances
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
#if UNITY_2020_0_OR_NEWER
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
            if (audioClip == null)
            {
                Debug.LogError($"Csound.Unity.CopyFilesToPersistentDataPath Error: AudioClip at {origin} couldn't be loaded.");
                return;
            }

            var data = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(data, 0);

            WAFU.WriteAudioFile(audioClip, destination, 16);
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
            Stream s = new MemoryStream(bytes);
            BinaryReader br = new BinaryReader(s);
            var dir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using (BinaryWriter bw = new BinaryWriter(File.Open(destination, FileMode.OpenOrCreate)))
            {
                bw.Write(br.ReadBytes(bytes.Length));
            }
        }

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
    }
}
