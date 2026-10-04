using System;
using Csound.Unity;
using UnityEngine;
using Csound.Unity.NativeAudioInput;

public class NativeInputDebug : MonoBehaviour
{
    // For the per-second rates below.
    private float _lastRateTime;
    private ulong _lastCaptured;
    private long  _lastConsumed;
    private int    _lastFills;
    private double _lastDsp;
    private CsoundUnity _csound;
    private NativeAudioInputManager _mgr;
    private AudioSource _src;

    private int _dspLen;

    void Start()
    {

        _csound = GetComponent<CsoundUnity>();
        _mgr    = GetComponent<NativeAudioInputManager>();
        _src    = GetComponent<AudioSource>();

        // List all available input devices
        _mgr.EnumerateDevices();
        Debug.Log($"[NativeInputDebug] Found {_mgr.Devices.Count} input device(s):");
        for (int i = 0; i < _mgr.Devices.Count; i++)
        {
            var d = _mgr.Devices[i];
            Debug.Log($"  [{i}] {d.Name} | ch: {d.MaxChannelCount} | sr: {d.NominalSampleRate} Hz");
        }
    }

    private float _nextLog = 0f;

    void Update()
    {
        try
        {
            if (!_csound.IsInitialized)
            {
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 1f;
                    Debug.Log($"[t={Time.time:F1}s] CsoundUnity NOT initialized");
                }
                return;
            }
            if (Time.time < _nextLog) return;
            _nextLog = Time.time + 1f;

            ulong captured = 0;
            try   { captured = _mgr.FramesCaptured; }
            catch (Exception ex) { Debug.LogError($"[NativeInputDebug] FramesCaptured threw: {ex}"); }

            uint nchnls = 0;
            try   { nchnls = _csound.GetNchnlsInputs(); }
            catch (Exception ex) { Debug.LogError($"[NativeInputDebug] GetNchnlsInputs threw: {ex}"); }

            // Per-second rates, which is what actually settles whether we consume faster than the
            // device produces. The absolute totals hide it: both just go up.
            var now   = Time.time;
            var dt    = now - _lastRateTime;
            // Reopening the device restarts the native counters from zero, so a delta taken across a
            // reopen is negative. On the unsigned capture counter that wrapped to 1.8e19 and made the
            // first line after every configuration change unreadable. A reset is not a rate.
            var reopened = captured < _lastCaptured || _mgr.FramesConsumed < _lastConsumed;
            var capPs = dt > 0 && !reopened ? (captured - _lastCaptured) / dt : 0;
            var conPs = dt > 0 && !reopened ? (_mgr.FramesConsumed - _lastConsumed) / dt : 0;
            // Computed here with the others, NOT inside the log string below: the line that
            // refreshes _lastFills runs first, so an inline difference there is always zero.
            var fillPs = dt > 0 && _mgr.FillCalls >= _lastFills ? (_mgr.FillCalls - _lastFills) / dt : 0;
            // Unity's own clock against the wall. dspTime counts samples and divides by the rate
            // Unity believes it is running at, so if the hardware is faster than that belief this
            // ratio is the factor by which, measured without going through any of our code.
            // Re-read every line: the DSP block changes under a running scene, and a value cached
            // in Start made the column read 256 while Unity had moved to 1024 — which is exactly
            // the condition worth seeing.
            AudioSettings.GetDSPBufferSize(out _dspLen, out var _nb);
            var dspNow   = AudioSettings.dspTime;
            var dspRatio = dt > 0 ? (float)(dspNow - _lastDsp) / dt : 0f;
            _lastRateTime = now; _lastCaptured = captured; _lastConsumed = _mgr.FramesConsumed; _lastFills = _mgr.FillCalls; _lastDsp = dspNow;

            Debug.Log($"[t={Time.time:F1}s] State={_mgr.State} | " +
                      $"captured={captured} | " +
                      $"cap/s={capPs:F0} con/s={conPs:F0} fill/s={fillPs:F0} dsp/wall={dspRatio:F3} inRing={captured - (ulong)System.Math.Max(0, _mgr.FramesConsumed)} peak={_mgr.PeakInRing}/{_mgr.PrimeFrames} | " +
                      $"deviceRate={_mgr.EffectiveSampleRate:F0} unityRate={AudioSettings.outputSampleRate} csoundSr={_csound.GetSr():F0} dsp={_dspLen}x{_nb} | " +
                      // Ring health. starved = blocks where there was not a full ksmps ready and
                      // silence went to Csound; short = reads that came back with less than asked.
                      // A few starved at the start is the cushion filling. Both should then stop moving.
                      $"starved={_mgr.StarvedBlocks} short={_mgr.ShortReads} missing={_mgr.FramesMissing} dropped={_mgr.FramesDropped} | " +
                      $"latency={_mgr.InputLatencyMs:F1}ms | " +
                      $"nchnls_i={nchnls} | " +
                      $"srcPlaying={(_src != null ? _src.isPlaying.ToString() : "noSrc")}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NativeInputDebug] Update() threw: {ex}");
        }
    }
}
