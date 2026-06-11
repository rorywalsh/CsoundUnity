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

using System.Runtime.InteropServices;
using System.Text;

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// P/Invoke declarations for the native audio input plugins.
    /// The API surface is identical on macOS (CoreAudio) and Android (AAudio);
    /// only the library name differs.
    /// </summary>
    internal static class NativeAudioInputBridge
    {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS
        private const string LibName = "CsoundNativeInput";
#elif UNITY_ANDROID
        private const string LibName = "csnativeinput";
#else
        private const string LibName = "__Unsupported__";
#endif

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)

        /// <summary>
        /// Enumerates available audio input devices and caches them internally.
        /// Must be called before any other function.
        /// Returns the number of input devices found.
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_get_device_count();

        /// <summary>Copies the device name at <paramref name="deviceIndex"/> into <paramref name="outName"/>.</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern void cni_get_device_name(int deviceIndex,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder outName, int maxLen);

        /// <summary>Returns the maximum number of input channels for the given device.</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_get_device_channel_count(int deviceIndex);

        /// <summary>
        /// Returns the device's nominal sample rate in Hz, or 0 if not applicable
        /// (e.g. Android, where AAudio picks the rate automatically).
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern float cni_get_device_nominal_sample_rate(int deviceIndex);

        /// <summary>
        /// Opens the audio input device and starts capturing.
        /// </summary>
        /// <param name="deviceIndex">Index from the last <see cref="cni_get_device_count"/> call.</param>
        /// <param name="channelCount">Number of input channels to capture.</param>
        /// <param name="bufferSizeFrames">Requested I/O buffer size in frames (latency hint for classic fallback).</param>
        /// <param name="sampleRate">Expected sample rate — must match Unity's output rate.</param>
        /// <param name="ksmps">Csound ksmps — used as the IAudioClient3 engine period target.</param>
        /// <param name="exclusiveMode">1 = try WASAPI exclusive mode first (lowest latency,
        /// locks the device); 0 = shared mode (coexists with other apps). Windows only;
        /// ignored by the CoreAudio/AAudio backends.</param>
        /// <returns>0 on success, negative error code on failure.</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_open(int deviceIndex, int channelCount,
                                            int bufferSizeFrames, float sampleRate, int ksmps,
                                            int exclusiveMode);

        /// <summary>Stops capturing and releases all native resources.</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void cni_close();

        /// <summary>
        /// Reads captured audio from the internal ring buffer into <paramref name="outBuffer"/>.
        /// <para>Must be called from the Unity audio thread (inside OnAudioFilterRead / ProcessBlock).</para>
        /// Writes exactly <c>frameCount × channelCount</c> interleaved floats.
        /// On underrun, missing samples are zero-filled.
        /// </summary>
        /// <returns>Number of frames actually read from the ring buffer (remainder was zero-filled).</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int cni_read_frames(float* outBuffer,
                                                          int frameCount,
                                                          int channelCount);

        /// <summary>Returns the current input latency in frames (device latency + buffer size).</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_get_input_latency_frames();

        /// <summary>Returns 1 if the session is currently capturing, 0 otherwise.</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_is_running();

        /// <summary>
        /// Returns the total number of audio frames successfully captured by the native callback
        /// since the last <see cref="cni_open"/>. If this stays at 0 while running, call
        /// <see cref="cni_get_last_render_error"/> to get the AudioUnit render error code.
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong cni_get_frames_captured();

        /// <summary>
        /// Returns the last OSStatus error returned by <c>AudioUnitRender</c> inside the capture callback.
        /// 0 = no error (render succeeding). Common non-zero values:
        /// <list type="bullet">
        ///   <item><description>-10863 (kAudioUnitErr_NoConnection): device not bound before stream format was set, or microphone permission denied.</description></item>
        ///   <item><description>-10877 (kAudioComponentErr_InstanceInvalidated): device disconnected.</description></item>
        /// </list>
        /// Reset to 0 on <see cref="cni_open"/> / <see cref="cni_close"/>.
        /// macOS / iOS / visionOS only; always returns 0 on other platforms.
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cni_get_last_render_error();

#endif // (macOS || Windows || Android) && (!WebGL || Editor)

#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_WEBGL
        /// <summary>
        /// Returns the number of times <see cref="cni_read_frames"/> could not supply a full
        /// ksmps block of audio (ring buffer underrun → zero-filled gap → audible click).
        /// Reset to 0 on <see cref="cni_open"/> and <see cref="cni_close"/>.
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint cni_get_underrun_count();

        /// <summary>
        /// Returns the number of times the AAudio callback found the ring buffer full and
        /// had to drop incoming audio (overrun → missing audio segment → audible dropout).
        /// Reset to 0 on <see cref="cni_open"/> and <see cref="cni_close"/>.
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint cni_get_overrun_count();

        /// <summary>Resets both underrun and overrun counters to zero.</summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void cni_reset_xrun_counts();
#endif // UNITY_ANDROID && !UNITY_EDITOR
    }
}
