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

using UnityEngine;

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// Cross-platform audio input coordinator for CsoundUnity.
    ///
    /// <para>Add this component to the same GameObject as <see cref="CsoundUnity"/>,
    /// together with both <see cref="NativeAudioInputManager"/> and <see cref="WebGLAudioInput"/>.
    /// In <c>Awake</c> the router enables the appropriate component for the current
    /// platform and disables the other, so the same scene works in the Unity Editor,
    /// on desktop / mobile, and in a WebGL build without any manual changes.</para>
    ///
    /// <para>Recommended Inspector setup on the GameObject:</para>
    /// <list type="number">
    ///   <item><see cref="CsoundUnity"/> — assign the CSD as usual.</item>
    ///   <item><see cref="NativeAudioInputManager"/> — set channels / buffer as needed;
    ///     leave <c>Open On Initialized</c> as desired (the router does not override it).</item>
    ///   <item><see cref="WebGLAudioInput"/> — set channels as needed;
    ///     leave <c>Open On Initialized</c> as desired.</item>
    ///   <item><see cref="CsoundUnityAudioInputRouter"/> — just add it; no configuration required.</item>
    /// </list>
    ///
    /// <para>Platform routing:</para>
    /// <list type="bullet">
    ///   <item><b>Editor / Standalone / iOS / Android / visionOS</b>:
    ///     <see cref="NativeAudioInputManager"/> enabled, <see cref="WebGLAudioInput"/> disabled.</item>
    ///   <item><b>WebGL build</b>:
    ///     <see cref="WebGLAudioInput"/> enabled, <see cref="NativeAudioInputManager"/> disabled
    ///     (its <c>Open()</c> is a no-op on WebGL anyway, but disabling it avoids the warning log).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-50)] // run before NativeAudioInputManager and WebGLAudioInput
    public class CsoundUnityAudioInputRouter : MonoBehaviour
    {
        private void Awake()
        {
            var native = GetComponent<NativeAudioInputManager>();
            var webgl  = GetComponent<WebGLAudioInput>();

            if (native == null && webgl == null)
            {
                Debug.LogWarning("[CsoundUnityAudioInputRouter] No NativeAudioInputManager or WebGLAudioInput " +
                                 "found on this GameObject. Add at least one of them.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL build: use browser getUserMedia via WebGLAudioInput.
            if (native != null) native.enabled = false;
            if (webgl  != null) webgl.enabled  = true;
            else Debug.LogWarning("[CsoundUnityAudioInputRouter] WebGL build detected but no WebGLAudioInput " +
                                  "component found. Add one to this GameObject for microphone access.");
#else
            // Editor / native platform: use NativeAudioInputManager.
            if (webgl  != null) webgl.enabled  = false;
            if (native != null) native.enabled = true;
            else Debug.LogWarning("[CsoundUnityAudioInputRouter] No NativeAudioInputManager found. " +
                                  "Add one to this GameObject for native audio input.");
#endif
        }
    }
}
