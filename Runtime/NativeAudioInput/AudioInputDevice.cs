namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// Platform-agnostic descriptor for an audio input device returned by
    /// <see cref="NativeAudioInputManager.Devices"/>.
    /// </summary>
    public readonly struct AudioInputDevice
    {
        /// <summary>Zero-based index passed to <see cref="NativeAudioInputManager.Open"/>.</summary>
        public readonly int    Index;
        /// <summary>Human-readable name (e.g. "Built-in Microphone", "USB Audio CODEC").</summary>
        public readonly string Name;
        /// <summary>Maximum number of input channels this device supports.</summary>
        public readonly int    MaxChannelCount;
        /// <summary>
        /// Device's nominal sample rate in Hz.
        /// 0 on Android (AAudio chooses the rate automatically to match hardware).
        /// </summary>
        public readonly float  NominalSampleRate;

        public AudioInputDevice(int index, string name, int maxChannelCount, float nominalSampleRate)
        {
            Index              = index;
            Name               = name;
            MaxChannelCount    = maxChannelCount;
            NominalSampleRate  = nominalSampleRate;
        }

        public override string ToString() =>
            $"[{Index}] {Name} ({MaxChannelCount}ch{(NominalSampleRate > 0 ? $", {NominalSampleRate:F0} Hz" : "")})";
    }
}
