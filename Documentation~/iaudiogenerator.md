## IAudioGenerator (Unity 6+) ##

From v4.0.0, CsoundUnity supports the **IAudioGenerator** interface introduced in Unity 6. This is the default audio path for new components on Unity 6 and above.

### What is IAudioGenerator? ###

`IAudioGenerator` is a Unity 6 API that allows a custom component to drive an `AudioSource` directly, bypassing the `OnAudioFilterRead` pipeline. CsoundUnity uses it to call `csoundPerformKsmps()` in the audio thread's `Process()` callback, writing samples directly into Unity's output buffer.

**Benefits over OnAudioFilterRead:**

- Lower and more predictable latency
- Tighter synchronisation with Unity's audio graph
- The `AudioSource` is the true audio producer — no filter chain is involved
- DSP load measurement is more accurate

### Selecting the audio path ###

In the CsoundUnity inspector, the **Audio Path** field (Unity 6 only) lets you switch between:

- **IAudioGenerator** — default on Unity 6+
- **OnAudioFilterRead** — classic path, available on all Unity versions

The field is hidden during Play mode; changes take effect on the next initialisation.

### CsoundUnityGenerator ###

For users who prefer a leaner component, `CsoundUnityGenerator` is a standalone `MonoBehaviour` that implements only the `IAudioGenerator` interface. It has no `OnAudioFilterRead` fallback and is designed exclusively for Unity 6+.

Add it to a GameObject that already has an `AudioSource`, assign a `.csd` file, and it will compile and run Csound using the IAudioGenerator path.

### Startup delay ###

On Unity 6 with IAudioGenerator, there is a brief period between scene start and when the `AudioSource` begins requesting audio. The **Generator Startup Delay** field (default: 0.1 s) ensures Csound has time to initialise before the first audio callback arrives. Increase this value if you hear glitches at startup on slower devices.

### ksmps and buffer size ###

With IAudioGenerator, `ksmps` is passed directly to the Csound engine without rounding. The Unity audio buffer size (typically 512 samples) must be a multiple of `ksmps` for glitch-free performance. CsoundUnity logs a warning if this is not the case.

Recommended `ksmps` values for common Unity buffer sizes:

| Unity buffer | Recommended ksmps |
|---|---|
| 256 | 1, 2, 4, 8, 16, 32, 64, 128, 256 |
| 512 | 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 |
| 1024 | 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 |

### Callbacks ###

The IAudioGenerator path fires two additional callbacks per audio block, accessible via `CsoundBridgeRegistry`:

- `OnSpinFillCallback` — called before each `PerformKsmps`, used by Audio Input Routing to fill the spin buffer
- `OnKsmpsCallback` — called after each `PerformKsmps`, used to read the output buffer

These are internal hooks used by the routing system; most users will not need to interact with them directly.

### Compatibility ###

`IAudioGenerator` features are compiled only on Unity 6 (`#if UNITY_6000_0_OR_NEWER`). On earlier Unity versions, CsoundUnity automatically falls back to `OnAudioFilterRead` regardless of the serialised `AudioPath` value.
