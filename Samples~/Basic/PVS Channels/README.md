# PVS channels

Open **PvsChannels.unity** and enter Play mode. Requires the native Csound bridge; WebGL displays
`UnsupportedPlatform` and leaves the CSD uninitialized.

1. Enable **Listen**. The saw wave is analyzed by `pvsanal`, read by C#, filtered, returned to Csound
   and resynthesized by `pvsynth`.
2. Move **Source frequency**, **C# cutoff** and **C# spectral gain**. Frequencies above the cutoff
   have their bin amplitudes zeroed in C#; remaining amplitudes are scaled by the gain.
3. Toggle **Hear C# processed spectrum** to compare the returned spectrum with the original saw.
   Amber bars show the original magnitudes and cyan bars show the processed values (0–8 kHz,
   -80 to 0 dB). Analysis/resynthesis introduces latency, so the two listening paths are not aligned.
4. Pause/resume and restart. Frame counters freeze on pause; restart reinitializes the input channel
   and starts instrument 1 only after its metadata exists.

The frame contains 513 amplitude/frequency pairs: 1026 **float** entries, regardless of native audio
sample precision. `Overlap = 256` is the hop in audio samples. `FrameCount` identifies a new spectrum,
not a control period. The audio-thread callback processes each new frame once and preserves its
counter when writing the matching destination layout. Main-thread visualization uses its own buffers
and can miss frames. Scratch arrays are reused; GUI drawing/formatting allocates independently.

PVS and array operations share a nonblocking host gate; `Busy` may occur under contention. The
callback retries on a later control period, without a loop or extra PerformKsmps call. This sample
is a simple bin mask, not a general-purpose high-quality filter. FFT/hop/window/format stay fixed.
Sliding spectra and partial tracks are not supported by this API.

The **API Smoke Test** in Diagnostics checks the same public API with repeatable pass/fail results.
