using System;
using Csound.Unity;
using UnityEngine;
using Csound.Unity.NativeAudioInput;

public class NativeInputDebug : MonoBehaviour
{
    private CsoundUnity _csound;
    private NativeAudioInputManager _mgr;
    private AudioSource _src;

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

            Debug.Log($"[t={Time.time:F1}s] State={_mgr.State} | " +
                      $"captured={captured} | " +
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
