using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Csound.Unity.Utilities.LoadFiles
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
        [Tooltip("The names of the plugins to copy from Resources to the Persistent Data Path folder. " +
            "Don't specify the extension. The extension will be added to the copied files depending on the platform. ")]
        [SerializeField] private string[] _pluginsNames;
        [Tooltip("Those files will be read from the StreamingAssets folder")]
        [SerializeField] private string[] _streamingAssetsFiles;
        [Tooltip("Those files will be read from Resources folders")]
        [SerializeField] private AdditionalFileInfo[] _additionalFiles;

        [Tooltip("Ensure these CsoundUnity GameObjects are inactive when hitting play, " +
            "otherwise their initialization will run. " +
            "Setting the Environment Variables on a running Csound instance can have unintended effects.")]
        [SerializeField] private CsoundUnity[] _csoundUnitys;

        private int _filesToCopy;
        private int _copiedFiles;

        void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            _filesToCopy = _streamingAssetsFiles.Length + _additionalFiles.Length;
#else
            _filesToCopy = _pluginsNames.Length + _streamingAssetsFiles.Length + _additionalFiles.Length;
#endif

            Debug.Log($"Csound.Unity.LoadFiles: Copying {_filesToCopy} files to Persistent Data Path");

#if !UNITY_ANDROID && !UNITY_IOS
            foreach (var pluginName in _pluginsNames)
            {
                Debug.Log($"Csound.Unity.LoadFiles: Copying plugin: {pluginName}");
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
                Debug.Log($"Csound.Unity.LoadFiles: File Exists? {File.Exists(destinationPath)}");
                if (!File.Exists(destinationPath))
                {
                    Debug.Log($"Csound.Unity.LoadFiles: Loading plugin at path: {pluginPath}");
                    var plugin = Resources.Load<TextAsset>(pluginPath);
                    Debug.Log($"Csound.Unity.LoadFiles: Loaded plugin bytes: {plugin.bytes.Length}");
                    Debug.Log($"Csound.Unity.LoadFiles: Writing plugin file at path: {destinationPath}");
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
                    Directory.CreateDirectory(dir);

                var filePath = Path.Combine(additionalFile.Directory, additionalFile.FileName + "." + additionalFile.Extension);
                var destinationPath = Path.Combine(dir, additionalFile.FileName + "." + additionalFile.Extension);

                Debug.Log($"Csound.Unity.LoadFiles: Copying additional File from Resources: {additionalFile.FileName}, destinationPath: {destinationPath}, file Exists? {File.Exists(destinationPath)}");
                if (!File.Exists(destinationPath))
                {
                    CopyGenericFileFromResources(filePath, destinationPath);
                }
                _copiedFiles++;
            }

            // start waiting for all the files to be copied, at the end enable all the CsoundUnity instances
            StartCoroutine(WaitForCopy());
        }

        IEnumerator WaitForCopy()
        {
            while (_copiedFiles < _filesToCopy)
            {
                yield return null;
            }
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
                if (req.isHttpError || req.isNetworkError)
                {
                    Debug.LogError($"Csound.Unity.LoadFiles Error: {req.error}");
                    yield break;
                }
                Debug.Log($"Csound.Unity.LoadFiles: {req.downloadHandler.data.Length} bytes read");
                onBytesLoaded?.Invoke(req.downloadHandler.data);
#endif
            }
        }

        private static void CopyGenericFileFromResources(string origin, string destination)
        {
            var pathWithoutExtension = Path.ChangeExtension(origin, null);
            var textAsset = Resources.Load<TextAsset>(pathWithoutExtension);
            WriteFile(textAsset.bytes, destination);
        }

        private static void WriteFile(byte[] bytes, string destination)
        {
            Debug.Log($"Csound.Unity.LoadFiles: Writing file ({bytes.Length} bytes) at path: {destination}");
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
            public string Directory;
            public string FileName;
            public string Extension;
        }
    }
}
