using System.Collections;
using System.IO;
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
    /// 8. Wait 2s  → LoadCsdFromPath(): writes a csd to persistentDataPath and loads it back
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

        /// <summary>
        /// A second csd, written to disk and read back so LoadCsdFromPath has a real file to open.
        /// A bell rather than the saw pair of RuntimeCsd, so the swap is audible and not just
        /// visible in the log.
        /// </summary>
        private const string FileCsd = @"
<Cabbage>
form caption(""Csd From File"") size(300, 100)
hslider bounds(0, 0, 300, 50) range(0.5, 12, 3.5, 1, 0.01) channel(""ratio"") text(""Ratio"")
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
    kratio chnget ""ratio""
    aenv   expon 0.3, p3, 0.001
    amod   oscili aenv * p4 * 2, p4 * kratio
    abell  oscili aenv, p4 + amod
    outs abell, abell
endin
</CsInstruments>
<CsScore>
i1 0.0 1.2 440
i1 0.7 1.2 587
i1 1.4 1.8 330
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
            if (!_csound.LoadCsdFromString(RuntimeCsd))
            {
                Debug.LogError("[LifecycleDemo] LoadCsdFromString did not end up running — "
                             + "look above for a Csound compilation error.");
                yield break;
            }

            // The channels come from the string, not from the csd asset: "cutoff" exists only
            // here, and "freqSlider" from BasicTest.csd is gone.
            var channels = _csound.channels;
            Debug.Log($"[LifecycleDemo] Runtime csd running — {channels.Count} channel(s) parsed " +
                      $"from the string: {string.Join(", ", channels.ConvertAll(c => c.channel))}");

            _csound.SetChannel("cutoff", _runtimeCutoff);
            Debug.Log($"[LifecycleDemo] cutoff set to {_runtimeCutoff}");

            // --- 8. Write a csd to disk and load it back by path, WHILE IT IS STILL PLAYING ---
            // Deliberately not waiting for the natural end here. A performance that ends on its
            // own is stopped by CsoundUnity, so every earlier step went through Initialize();
            // cutting in mid-score is what exercises the other branch, where LoadCsd* finds the
            // instance running and reloads it with Restart(). That is also the realistic case:
            // swapping a patch rarely waits politely for the score to finish.
            //
            // persistentDataPath is a real, writable directory on every platform, so this step
            // exercises the synchronous overload on device too. StreamingAssets is the case that
            // needs the callback overload instead — see lifecycle.md.
            yield return new WaitForSeconds(1.5f);

            var path = Path.Combine(Application.persistentDataPath, "LifecycleDemoRuntime.csd");
            File.WriteAllText(path, FileCsd);
            Debug.Log($"[LifecycleDemo] Wrote csd to {path}");

            Debug.Log($"[LifecycleDemo] Csound running? {_csound.IsInitialized} " +
                      $"— so LoadCsdFromPath will go through {(_csound.IsInitialized ? "Restart()" : "Initialize()")}");

            _performanceFinished = false;
            if (!_csound.LoadCsdFromPath(path))
            {
                Debug.LogError("[LifecycleDemo] LoadCsdFromPath failed — see the error above.");
                yield break;
            }

            var fileChannels = _csound.channels;
            Debug.Log($"[LifecycleDemo] Csd from file running — {fileChannels.Count} channel(s): " +
                      $"{string.Join(", ", fileChannels.ConvertAll(c => c.channel))}");

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
