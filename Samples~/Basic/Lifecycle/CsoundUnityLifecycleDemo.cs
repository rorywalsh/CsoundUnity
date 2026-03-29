using System.Collections;
using UnityEngine;

namespace Csound.Unity.Samples
{
    /// <summary>
    /// Demonstrates CsoundUnity lifecycle features:
    /// - Delayed initialization (initializeOnAwake = false)
    /// - Manual Initialize(), Stop() and Restart()
    /// - Natural performance end detection via OnCsoundPerformanceFinished
    ///
    /// Timeline (with default values):
    /// 1. Wait 2s  → Initialize()
    /// 2. Wait 3s  → Manual Stop() (interrupts the CSD mid-play)
    /// 3. Wait 2s  → Restart()
    /// 4. OnCsoundPerformanceFinished → natural end detected, CsoundUnity auto-stops
    /// 5. Wait 2s  → Restart()
    /// 6. OnCsoundPerformanceFinished → natural end detected, done
    ///
    /// Setup:
    /// 1. Attach this component to the same GameObject as CsoundUnity
    /// 2. Uncheck "Initialize On Awake" on CsoundUnity to use delayed init
    /// </summary>
    public class CsoundUnityLifecycleDemo : MonoBehaviour
    {
        [SerializeField] CsoundUnity _csound;

        [Header("Delayed Init")]
        [Tooltip("Seconds to wait before calling Initialize() when initializeOnAwake is false")]
        [SerializeField] float _initDelay = 2f;

        [Header("Stop / Restart")]
        [Tooltip("Seconds after init before calling the manual Stop()")]
        [SerializeField] float _stopDelay = 3f;
        [Tooltip("Seconds to wait after a stop before calling Restart()")]
        [SerializeField] float _restartDelay = 2f;

        private bool _performanceFinished;

        IEnumerator Start()
        {
            if (_csound == null)
                _csound = GetComponent<CsoundUnity>();

            _csound.OnCsoundInitialized += () => Debug.Log("[LifecycleDemo] OnCsoundInitialized fired");
            _csound.OnCsoundStopped += () => Debug.Log("[LifecycleDemo] OnCsoundStopped fired");
            _csound.OnCsoundPerformanceFinished += OnPerformanceFinished;

            // --- 1. Delayed init ---
            if (!_csound.initializeOnAwake)
            {
                Debug.Log($"[LifecycleDemo] Waiting {_initDelay}s before Initialize()...");
                yield return new WaitForSeconds(_initDelay);
                Debug.Log("[LifecycleDemo] Calling Initialize()...");
                _csound.Initialize();
            }

            yield return new WaitUntil(() => _csound.IsInitialized);
            Debug.Log("[LifecycleDemo] Csound running.");

            // --- 2. Manual stop after _stopDelay ---
            Debug.Log($"[LifecycleDemo] Waiting {_stopDelay}s before manual Stop()...");
            yield return new WaitForSeconds(_stopDelay);
            Debug.Log("[LifecycleDemo] Calling Stop() manually...");
            _csound.Stop();

            // --- 3. Restart after _restartDelay ---
            Debug.Log($"[LifecycleDemo] Waiting {_restartDelay}s before Restart()...");
            yield return new WaitForSeconds(_restartDelay);
            Debug.Log("[LifecycleDemo] Calling Restart()...");
            _performanceFinished = false;
            _csound.Restart();

            yield return new WaitUntil(() => _csound.IsInitialized);
            Debug.Log("[LifecycleDemo] Csound running. Waiting for natural end...");

            // --- 4. Wait for natural performance end (OnCsoundPerformanceFinished auto-stops) ---
            yield return new WaitUntil(() => _performanceFinished);
            Debug.Log("[LifecycleDemo] Performance finished naturally. CsoundUnity stopped.");

            // --- 5. Restart again after _restartDelay ---
            Debug.Log($"[LifecycleDemo] Waiting {_restartDelay}s before final Restart()...");
            yield return new WaitForSeconds(_restartDelay);
            Debug.Log("[LifecycleDemo] Calling Restart()...");
            _performanceFinished = false;
            _csound.Restart();

            yield return new WaitUntil(() => _csound.IsInitialized);
            Debug.Log("[LifecycleDemo] Csound running. Waiting for natural end...");

            // --- 6. Wait for final natural end ---
            yield return new WaitUntil(() => _performanceFinished);
            Debug.Log("[LifecycleDemo] Demo complete!");
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundPerformanceFinished -= OnPerformanceFinished;
        }

        private void OnPerformanceFinished()
        {
            Debug.Log("[LifecycleDemo] OnCsoundPerformanceFinished fired");
            _performanceFinished = true;
        }
    }
}
