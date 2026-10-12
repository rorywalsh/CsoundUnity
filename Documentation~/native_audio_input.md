## Native Audio Input ##

> **New in v4.0.0**

NativeAudioInput provides low-latency, multi-channel audio input by calling directly into platform-native APIs (CoreAudio, AAudio, WASAPI, AudioUnit RemoteIO) rather than going through Unity's Microphone API. Captured samples are written into the Csound spin buffer every ksmps cycle, so they are available to any instrument that reads `inch`.

---

### Supported platforms ###

| Platform | Backend | Notes |
|---|---|---|
| **macOS** | CoreAudio / AUHAL | Multi-channel, device selection, hardware buffer control |
| **iOS** | AudioUnit RemoteIO | Multi-channel, AVAudioSession |
| **visionOS** | — | **not shipped yet**: Csound runs there, but the native input plugin is not built for it |
| **Android (API ≥ 26)** | AAudio exclusive mode | ≈4 ms of **input** latency @ 48 kHz on supported hardware |
| **Android (API < 26)** | Unity Microphone (fallback) | ≈50 ms of **input** latency; mono only |
| **Windows** | WASAPI | Exclusive mode (lowest latency) or shared mode |
| **WebGL / Linux** | Not supported | — |

---

### Setup ###

1. Add a **NativeAudioInputManager** component to the same GameObject as your `CsoundUnity` instance (or assign the `CsoundUnity` reference manually in the inspector).
2. Configure the inspector fields (device, channel count, buffer size).
3. Either enable **Open On Initialized** to start automatically when Csound is ready, or call `Open()` from code at the right moment.

#### Minimum latency on Android ####

Set **Project Settings → Audio → System Sample Rate** to `48000`. AAudio EXCLUSIVE mode maps the callback directly to hardware DMA at this rate.

#### Microphone permission ####

On **iOS** CsoundUnity requests the microphone permission automatically via a coroutine before opening the device. Add a `NSMicrophoneUsageDescription` entry to your `Info.plist`.

On **macOS** (standalone builds), the system will prompt the user. Add `com.apple.security.device.audio-input` to your entitlements file.

On **Android**, `RECORD_AUDIO` is a *dangerous* permission: from API 23 it has to be granted by the
user at runtime, not at install. Nothing extra is normally needed, because the package references
Unity's `Microphone` class, so Unity adds the permission to the manifest it generates and — with its
default settings — asks for it when the app starts.

If you suppress that startup prompt (`unityplayer.SkipPermissionsDialog` in your own manifest), ask
for the permission yourself before calling `Open()`, or the device will fail to open.

---

### Inspector fields ###

| Field | Description |
|---|---|
| **CsoundUnity** | The CsoundUnity instance to feed audio into |
| **Device Index** | Index into the device list returned by EnumerateDevices |
| **Channel Count** | Number of input channels to capture (1–32) |
| **Requested Buffer Frames** | Latency hint to the driver in frames (actual may differ; 256 is a good default) |
| **Open On Initialized** | Open automatically when CsoundUnity finishes initialising |
| **Exclusive Mode** | Windows only: WASAPI exclusive mode for minimum latency (locks the device) |

---

### Using input audio in Csound ###

Once NativeAudioInputManager is open, audio appears in the Csound spin buffer. Use the `inch` opcode to read it:

```csound
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
sr     = 48000
ksmps  = 128
nchnls = 2
0dbfs  = 1

instr InputPassthrough
    aInL inch 1    ; device channel 0
    aInR inch 2    ; device channel 1
    outs aInL, aInR
endin
</CsInstruments>

> **Counting.** Everything on the C# side — the device channel, the spin index, `AddInputSample` —
> counts from **0**. Csound's `inch` counts from **1**. So device channel 0 is `inch 1`, channel 1 is
> `inch 2`, and so on.

<CsScore>
i "InputPassthrough" 0 z
</CsScore>
```

Spin channel indices start at 1 in Csound (i.e. `inch 1` reads NativeAudioInput channel index 0).

---

### State and diagnostics ###

```csharp
var mgr = GetComponent<NativeAudioInputManager>();

// State: Stopped / Opening / Running / Fallback / Error
Debug.Log(mgr.State);

// How many frames the native callback has captured since Open()
// If this stays at 0 while Running, check permissions or device selection
Debug.Log(mgr.FramesCaptured);

// Input latency
Debug.Log($"{mgr.InputLatencyMs:F1} ms  ({mgr.InputLatencyFrames} frames)");

// Android only: ring-buffer health (0 = perfect)
Debug.Log($"underruns={mgr.UnderrunCount}  overruns={mgr.OverrunCount}");
```

---

### Code API ###

```csharp
using Csound.Unity.NativeAudioInput;

var mgr = GetComponent<NativeAudioInputManager>();

// Populate the Devices list before showing a device picker
mgr.EnumerateDevices();
foreach (var dev in mgr.Devices)
    Debug.Log($"[{dev.Index}] {dev.Name}  {dev.MaxChannels}ch  {dev.NominalSampleRate} Hz");

// Open device 0, 2 channels, 256-frame buffer
mgr.Open(deviceIndex: 0, channelCount: 2, bufferFrames: 256);

// Stop and release
mgr.Close();
```

`Open()` closes any previously open session first, so it is safe to call repeatedly.

---

### Platform notes ###

#### Android — AAudio exclusive mode ####

AAudio maps the callback directly to hardware DMA without mixing through AudioFlinger. Falls back
gracefully to Unity's Microphone API if the device does not support API 26 or if exclusive mode is
unavailable.

> **Where the numbers come from.** These are **input** latency — microphone to Csound — and neither
> is a measurement. On AAudio it is two hardware bursts, which the plugin reports as the stream's
> latency: at a 96-frame burst and 48 kHz that is ≈4 ms. For the Microphone fallback it is a fixed
> 50 ms the code assumes, since Unity does not report one. Output latency is separate and depends on
> the audio path — [Audio Output Path](audio_output_path.md#measured-latency) has figures that *were*
> measured. Round-trip is the two together, plus whatever the device adds, and has not been measured
> on Android.

#### macOS — CoreAudio / AUHAL ####

Uses an Audio Unit HAL Output component in input-only mode. Supports any Core Audio device visible in macOS Audio MIDI Setup, including USB audio interfaces with multiple input channels. Set `ksmps` to a divisor of the hardware buffer size for minimum jitter.

#### iOS — AudioUnit RemoteIO ####

Wraps `AudioUnit` with `kAudioOutputUnitProperty_EnableIO` on the input bus. AVAudioSession is configured for `PlayAndRecord` with `DefaultToSpeaker` and `AllowBluetooth` options. The `Requested Buffer Frames` setting maps to `AVAudioSession.preferredIOBufferDuration`.

#### Windows — WASAPI ####

Exclusive mode locks the device to the requested sample rate and buffer size, bypassing the Windows audio mixer. If exclusive mode is unavailable (device already in use), the plugin falls back to shared mode automatically. `ksmps` should divide evenly into the WASAPI engine period for glitch-free operation.
