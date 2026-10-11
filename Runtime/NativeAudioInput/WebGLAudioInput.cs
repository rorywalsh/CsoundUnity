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

using System.Collections;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// WebGL audio input: routes a browser microphone stream into the Csound AudioWorklet.
    ///
    /// <para>This component compiles on all platforms so it can coexist with
    /// <see cref="NativeAudioInputManager"/> in the same scene.
    /// Use <see cref="CsoundAudioInputRouter"/> to automatically activate the correct
    /// component at runtime based on the current platform.</para>
    ///
    /// <para>On non-WebGL platforms <see cref="Open"/> is a no-op (logs a warning).
    /// On WebGL, the CSD must declare <c>nchnls_i ≥ 1</c> and use <c>adc</c>
    /// (e.g. <c>ain, adc</c>). Audio routing is handled by the browser's Web Audio graph;
    /// <see cref="NativeAudioInputManager"/> is not used.</para>
    ///
    /// <para><b>Multi-channel limitation</b>: browsers cap <c>getUserMedia</c> at stereo
    /// (2 channels) regardless of the connected hardware. CSD opcodes reading
    /// <c>nchnls_i &gt; 2</c> will receive silence on extra channels.</para>
    ///
    /// <para>The browser shows a microphone permission prompt on the first <see cref="Open"/>
    /// call. Serve your build over HTTPS — <c>getUserMedia</c> fails on plain HTTP.</para>
    /// </summary>
    public class WebGLAudioInput : MonoBehaviour
    {
        #region Inspector

        [SerializeField] private CsoundUnity _csound;

        [Tooltip("If true, Open() is called automatically when CsoundUnity finishes initialising. " +
                 "Uses the browser's default audio input device.")]
        [SerializeField] private bool _openOnInitialized = true;

        [Tooltip("Number of input channels to request from the browser (ideal hint). " +
                 "Browsers cap audio input at stereo (2 channels) regardless of hardware.")]
#pragma warning disable CS0414 // read inside #if UNITY_WEBGL block; unused on non-WebGL platforms
        [SerializeField, Range(1, 2)] private int _channelCount = 1;
#pragma warning restore CS0414

        #endregion

        #region Properties

        /// <summary>True after a successful <see cref="Open"/> call (WebGL only).</summary>
        public bool IsOpen { get; private set; }

        #endregion

        #region Unity messages

        private IEnumerator Start()
        {
            if (!_csound) _csound = GetComponent<CsoundUnity>();
            if (!_csound)
            {
                Debug.LogError("[WebGLAudioInput] No CsoundUnity component found. " +
                               "Attach this component to the same GameObject as CsoundUnity.");
                yield break;
            }
            yield return new WaitUntil(() => _csound.IsInitialized);
            if (_openOnInitialized) Open(string.Empty);
        }

        private void OnDestroy() => Close();

        #endregion

        #region Public API

        /// <summary>
        /// Requests microphone access from the browser and connects the stream to the
        /// Csound AudioWorklet so the CSD can read it via <c>adc</c>.
        /// No-op on non-WebGL platforms.
        /// </summary>
        /// <param name="deviceId">
        /// Browser deviceId from <c>navigator.mediaDevices.enumerateDevices()</c>,
        /// or empty string for the system default input.
        /// </param>
        /// <param name="channelCount">
        /// Requested input channels (1–2). Browsers cap at stereo.
        /// Pass -1 to use the Inspector value.
        /// </param>
        public void Open(string deviceId = "", int channelCount = -1)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_csound || !_csound.IsInitialized)
            {
                Debug.LogWarning("[WebGLAudioInput] Open() called before CsoundUnity is initialized.");
                return;
            }
            if (IsOpen) Close();
            int ch = channelCount > 0 ? Mathf.Clamp(channelCount, 1, 2) : Mathf.Clamp(_channelCount, 1, 2);
            _pendingInstances[_csound.InstanceId] = this;
            CsoundWebGL.NativeMethods.csoundAudioInputEnable(
                _csound.InstanceId,
                deviceId ?? string.Empty,
                ch,
                Marshal.GetFunctionPointerForDelegate(
                    (CsoundWebGL.CsoundAudioInputCallback)OnAudioInputResult
                ).ToInt32());
#else
            Debug.LogWarning("[WebGLAudioInput] Only supported on the WebGL platform. " +
                             "On desktop / mobile, use NativeAudioInputManager.");
#endif
        }

        /// <summary>Disconnects the microphone stream. Safe to call when already closed or on non-WebGL.</summary>
        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsOpen || !_csound) return;
            CsoundWebGL.NativeMethods.csoundAudioInputDisable(_csound.InstanceId);
            IsOpen = false;
#endif
        }

        #endregion

#if UNITY_WEBGL && !UNITY_EDITOR

        #region Static callback (AOT-safe)

        // Keyed by CsoundUnity instance id so multiple instances don't collide.
        private static readonly Dictionary<int, WebGLAudioInput> _pendingInstances =
            new Dictionary<int, WebGLAudioInput>();

        /// <summary>
        /// Called by the jslib via <c>dynCall_vii</c> after <c>getUserMedia</c> resolves.
        /// Must be static and tagged <see cref="AOT.MonoPInvokeCallbackAttribute"/> for IL2CPP.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(CsoundWebGL.CsoundAudioInputCallback))]
        private static void OnAudioInputResult(int instanceId, int success)
        {
            if (!_pendingInstances.TryGetValue(instanceId, out var inst)) return;
            _pendingInstances.Remove(instanceId);
            inst.IsOpen = success != 0;
            if (success != 0)
                Debug.Log($"[WebGLAudioInput] Microphone connected (instance {instanceId}).");
            else
                Debug.LogError("[WebGLAudioInput] Failed to open microphone. " +
                               "Check browser permissions and serve over HTTPS.");
        }

        #endregion

#endif // UNITY_WEBGL && !UNITY_EDITOR
    }
}
