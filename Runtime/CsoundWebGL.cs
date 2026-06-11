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

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Csound.Unity
{
    /// <summary>
    /// P/Invoke declarations for the CsoundUnity WebGL JavaScript plugin (csound.jslib).
    /// Uses Csound 7 via the @csound/browser WASM bundle.
    /// </summary>
    internal static class CsoundWebGL
    {
        private const string DLLVersion = "__Internal";

        internal delegate void CsoundInitializeCallback(int instanceId);
        internal delegate void CsoundSetOptionCallback(int instanceId, int res);
        internal delegate void CsoundStopCallback(int instanceId);
        internal delegate void CsoundGetChannelCallback(int instanceId, string channel, float value);
        /// <summary>Called by csoundAudioInputEnable after getUserMedia resolves.</summary>
        internal delegate void CsoundAudioInputCallback(int instanceId, int success);

        internal static class NativeMethods
        {
            #region Instantiation

            [DllImport(DLLVersion)]
            internal static extern void csoundInitialize(int id, int variation, string csdText,
                                                          string filesToLoad, int callback);

            #endregion Instantiation

            [DllImport(DLLVersion)]
            internal static extern void csoundSetChannel(int instanceId, string channel, float value);

            [DllImport(DLLVersion)]
            internal static extern void csoundGetChannel(int instanceId, string channel, int callback);

            [DllImport(DLLVersion)]
            internal static extern int csoundInputMessage(int instanceId, string scoreEvent);

            // ── MIDI ──────────────────────────────────────────────────────────

            /// <summary>
            /// Sends a raw 3-byte MIDI message to the Csound WASM instance.
            /// The CSD must use a MIDI-enabled instrument (e.g. massign or global MIDI opcodes).
            /// </summary>
            [DllImport(DLLVersion)]
            internal static extern void csoundSendMidiMessage(int instanceId, byte b0, byte b1, byte b2);

            /// <summary>
            /// Connects all available Web MIDI inputs to Csound via navigator.requestMIDIAccess().
            /// Also auto-connects devices plugged in after the call.
            /// If <paramref name="gameObjectName"/> is non-empty, each incoming MIDI message is
            /// forwarded to Unity via SendMessage(gameObjectName, methodName, "b0,b1,b2").
            /// Requires HTTPS; not supported on Firefox or Safari.
            /// </summary>
            [DllImport(DLLVersion)]
            internal static extern void csoundMidiEnable(int instanceId,
                string gameObjectName, string methodName);

            // ── AUDIO INPUT ───────────────────────────────────────────────────

            /// <summary>
            /// Requests microphone access (getUserMedia) and connects the stream to the
            /// Csound AudioWorklet node so CSD opcodes like <c>ain, adc</c> receive live audio.
            /// The CSD must declare <c>nchnls_i ≥ 1</c>.
            /// <para>
            /// <b>Browser limitation</b>: <c>getUserMedia</c> is capped at stereo (2 channels) on
            /// all current browsers; extra channels requested via <paramref name="channelCount"/>
            /// will silently be silent. Multi-channel audio interfaces are not accessible via
            /// the Web Audio API.
            /// </para>
            /// </summary>
            /// <param name="instanceId">CsoundUnity WebGL instance id.</param>
            /// <param name="deviceId">Browser deviceId from <c>enumerateDevices</c>, or empty for default.</param>
            /// <param name="channelCount">Requested input channels (ideal hint; capped at 2 by browsers).</param>
            /// <param name="callback"><see cref="CsoundAudioInputCallback"/> function pointer
            /// (use <c>Marshal.GetFunctionPointerForDelegate</c>).</param>
            [DllImport(DLLVersion)]
            internal static extern void csoundAudioInputEnable(int instanceId,
                string deviceId, int channelCount, int callback);

            /// <summary>
            /// Disconnects the microphone stream and stops all audio tracks.
            /// Safe to call when no stream is open.
            /// </summary>
            [DllImport(DLLVersion)]
            internal static extern void csoundAudioInputDisable(int instanceId);
        }
    }
}

#endif
