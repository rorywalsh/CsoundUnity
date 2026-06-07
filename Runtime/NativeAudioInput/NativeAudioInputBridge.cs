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

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_WEBGL

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
        /// since the last <see cref="cni_open"/>. If this stays at 0 while running, the AudioUnit
        /// is not receiving data (wrong device, permission denied, or hardware error).
        /// </summary>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong cni_get_frames_captured();

#endif // (macOS || Windows || Android) && !WebGL
    }
}
