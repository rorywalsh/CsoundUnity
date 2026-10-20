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
    /// WebGL audio input: the browser microphone, as seen from C#.
    ///
    /// <para><b>An orchestra that reads input already has a microphone.</b> When the CSD declares
    /// <c>nchnls_i ≥ 1</c> and reads <c>adc</c>, Csound opens the device itself inside its own
    /// <c>start()</c> — and in the worker variations it refuses to do it any later — so
    /// <see cref="Open"/> reports the stream that is already running rather than opening a second
    /// one. That is what <see cref="IsOpen"/> answers for, and it is the only account of the browser
    /// input that C# has: on WebGL no handle exists to ask Csound for a channel count or a rate.
    /// A CSD that does not read input is the case where this component opens a stream itself, and
    /// where a <c>deviceId</c> or a channel count has any effect.</para>
    ///
    /// <para>This component compiles on all platforms so it can coexist with
    /// <see cref="CsoundUnityNativeAudioInput"/> in the same scene.
    /// Use <see cref="CsoundUnityAudioInputRouter"/> to automatically activate the correct
    /// component at runtime based on the current platform.</para>
    ///
    /// <para>On non-WebGL platforms <see cref="Open"/> is a no-op (logs a warning).
    /// Audio routing is handled by the browser's Web Audio graph;
    /// <see cref="CsoundUnityNativeAudioInput"/> is not used.</para>
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

        [Tooltip("Seconds to wait for the browser's audio context to start before giving up on " +
                 "opening the microphone. The context starts on the page's first click.")]
        [SerializeField] private float _audioContextTimeout = 30f;
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

#if UNITY_WEBGL && !UNITY_EDITOR
            // Initialised is not the same as audible. A browser keeps the AudioContext suspended
            // until the page has been clicked, and while it is suspended Csound's worklet node is
            // never registered: the microphone call would find nothing to connect to and report a
            // permission problem for a permission it never asked for. So wait for the context to be
            // running, which is to say wait for the page's first click, tap or key press.
            var waited = 0f;
            while (CsoundWebGL.NativeMethods.csoundIsAudioContextRunning(_csound.InstanceId) == 0)
            {
                if (waited > _audioContextTimeout)
                {
                    Debug.LogWarning("[WebGLAudioInput] Csound's audio context is still not running " +
                                     $"after {_audioContextTimeout:0} s, so the microphone was not " +
                                     "opened. A browser starts audio only after the page has been " +
                                     "clicked; call Open() yourself once it has.");
                    yield break;
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
#endif

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
                             "On desktop / mobile, use CsoundUnityNativeAudioInput.");
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
                Debug.LogError("[WebGLAudioInput] Failed to open microphone. The [CsoundUnity] line " +
                               "logged just above says why: the browser denied access, the page is " +
                               "served over plain HTTP from somewhere other than localhost, or " +
                               "Csound has nothing to connect the stream to.");
        }

        #endregion

#endif // UNITY_WEBGL && !UNITY_EDITOR
    }
}
