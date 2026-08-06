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
    /// 7. Wait 2s  → LoadCsdFromString(): swaps in a csd written in code, no asset involved
    ///
    /// Setup:
    /// 1. Attach this component to the same GameObject as CsoundUnity
    /// 2. Uncheck "Initialize On Awake" on CsoundUnity to use delayed init
    /// </summary>
    public class CsoundUnityLifecycleDemo : MonoBehaviour
    {
        #region Fields
        [SerializeField] CsoundUnity _csound;

        [Header("Delayed Init")]
        [Tooltip("Seconds to wait before calling Initialize() when initializeOnAwake is false")]
        [SerializeField] float _initDelay = 2f;

        [Header("Stop / Restart")]
        [Tooltip("Seconds after init before calling the manual Stop()")]
        [SerializeField] float _stopDelay = 3f;
        [Tooltip("Seconds to wait after a stop before calling Restart()")]
        [SerializeField] float _restartDelay = 2f;

        [Header("Load Csd From String")]
        [Tooltip("Cutoff to set on the channel that only the runtime csd declares")]
        [SerializeField] float _runtimeCutoff = 1200f;

        private bool _performanceFinished;

        /// <summary>
        /// A complete csd as a plain string: different instrument, different channels, and a
        /// finite score so the natural end fires here too. Nothing on disk, no asset, no GUID —
        /// which is the point of LoadCsdFromString. Quotes are doubled because of the verbatim
        /// string, they reach Csound as single ones.
        /// </summary>
        private const string RuntimeCsd = @"
<Cabbage>
form caption(""Runtime Csd"") size(300, 100)
hslider bounds(0, 0, 300, 50) range(100, 4000, 1200, 0.5, 1) channel(""cutoff"") text(""Cutoff"")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
sr     = 48000
ksmps  = 64
nchnls = 2
0dbfs  = 1

instr 1
    kcut  chnget ""cutoff""
    aenv  linsegr 0, 0.01, 0.3, 0.08, 0
    a1    vco2 1, p4
    a2    vco2 1, p4 * 1.008
    amix  moogladder (a1 + a2) * aenv * 0.5, kcut, 0.3
    outs amix, amix
endin
</CsInstruments>
<CsScore>
i1 0.0 0.4 220
i1 0.5 0.4 277
i1 1.0 0.4 330
i1 1.5 1.5 440
e 4
</CsScore>
</CsoundSynthesizer>
";
        #endregion

        #region Unity Messages
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

            // --- 7. Swap the csd for one built in code ---
            // LoadCsdFromString detaches the instance from its asset and reparses everything
            // from the string: channels, audio channels, nchnls and ksmps. On an instance that
            // is already running it restarts Csound, so no separate Restart() call is needed.
            Debug.Log($"[LifecycleDemo] Waiting {_restartDelay}s before LoadCsdFromString()...");
            yield return new WaitForSeconds(_restartDelay);

            Debug.Log("[LifecycleDemo] Calling LoadCsdFromString()...");
            _performanceFinished = false;
            _csound.LoadCsdFromString(RuntimeCsd);

            yield return new WaitUntil(() => _csound.IsInitialized);

            // The channels come from the string, not from the csd asset: "cutoff" exists only
            // here, and "freqSlider" from BasicTest.csd is gone.
            var channels = _csound.channels;
            Debug.Log($"[LifecycleDemo] Runtime csd running — {channels.Count} channel(s) parsed " +
                      $"from the string: {string.Join(", ", channels.ConvertAll(c => c.channel))}");

            _csound.SetChannel("cutoff", _runtimeCutoff);
            Debug.Log($"[LifecycleDemo] cutoff set to {_runtimeCutoff}");

            yield return new WaitUntil(() => _performanceFinished);
            Debug.Log("[LifecycleDemo] Demo complete!");
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundPerformanceFinished -= OnPerformanceFinished;
        }
        #endregion

        #region Private Helpers
        private void OnPerformanceFinished()
        {
            Debug.Log("[LifecycleDemo] OnCsoundPerformanceFinished fired");
            _performanceFinished = true;
        }
        #endregion
    }
}
