using System.Collections;
using UnityEngine;
using Csound.Unity.Utilities.MonoBehaviours;

namespace Csound.Unity.Samples.Environment
{
    /// <summary>Copies external files, then explicitly initializes an existing instance or a newly instantiated prefab.</summary>
    public class EnvironmentSampleInitializer : MonoBehaviour
    {
        [SerializeField] private CopyFilesToPersistentDataPath _copyFiles;
        [Tooltip("An existing instance with Initialize On Awake disabled. Leave Prefab empty when using this field.")]
        [SerializeField] private CsoundUnity _csoundUnity;
        [Tooltip("A prefab with Initialize On Awake disabled. Instantiated only after copying succeeds.")]
        [SerializeField] private CsoundUnity _csoundUnityPrefab;

        /// <summary>The existing instance or the prefab instance created after a successful copy.</summary>
        public CsoundUnity Instance { get; private set; }

        /// <summary>True after Csound reports initialization and the orchestra compiled successfully.</summary>
        public bool IsReady { get; private set; }

        private IEnumerator Start()
        {
            if (_copyFiles == null || (_csoundUnity == null) == (_csoundUnityPrefab == null))
            {
                Debug.LogError($"[Environment] {name}: assign the copier and exactly one Csound instance or prefab.");
                yield break;
            }
            var source = _csoundUnity != null ? _csoundUnity : _csoundUnityPrefab;
            if (source.initializeOnAwake)
            {
                Debug.LogError($"[Environment] {name}: disable Initialize On Awake before using manual startup.");
                yield break;
            }

            _copyFiles.Copy();
            while (_copyFiles.IsCopying) yield return null;
            if (!_copyFiles.copyCompleted)
            {
                Debug.LogWarning($"[Environment] {name}: copying did not succeed; Csound was not initialized.");
                yield break;
            }

            Instance = _csoundUnity != null ? _csoundUnity : Instantiate(_csoundUnityPrefab, transform);
            Instance.gameObject.SetActive(true);
            if (!Instance.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[Environment] {name}: activate the Csound object's parents before initialization.");
                yield break;
            }
            Debug.Log($"[Environment] {name}: files available; initializing Csound.");
            Instance.OnCsoundInitialized += OnInitialized;
            Instance.Initialize();
        }

        private void OnInitialized()
        {
            IsReady = Instance.HasCompiled;
            if (IsReady) Debug.Log($"[Environment] {name}: Csound initialized and orchestra compiled.");
            else Debug.LogError($"[Environment] {name}: Csound initialized without a compiled orchestra.");
        }

        private void OnDestroy()
        {
            if (Instance != null) Instance.OnCsoundInitialized -= OnInitialized;
        }
    }
}
