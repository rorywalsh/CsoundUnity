## Native Audio Input ##

> **New in v4.0.0**

NativeAudioInput provides low-latency, multi-channel audio input by calling directly into platform-native APIs (CoreAudio, AAudio, WASAPI, AudioUnit RemoteIO) rather than going through Unity's Microphone API. Captured samples are written into the Csound spin buffer every ksmps cycle, so they are available to any instrument that reads `inch`.

---

### Supported platforms ###

| Platform | Backend | Notes |
|---|---|---|
| **macOS** | CoreAudio / AUHAL | Multi-channel, device selection, hardware buffer control |
| **iOS / visionOS** | AudioUnit RemoteIO | Multi-channel, AVAudioSession |
| **Android (API ≥ 26)** | AAudio exclusive mode | ~4 ms @ 48 kHz on supported hardware |
| **Android (API < 26)** | Unity Microphone (fallback) | ~50 ms; mono only |
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

On **iOS and visionOS** CsoundUnity requests the microphone permission automatically via a coroutine before opening the device. Add a `NSMicrophoneUsageDescription` entry to your `Info.plist`.

On **macOS** (standalone builds), the system will prompt the user. Add `com.apple.security.device.audio-input` to your entitlements file.

On **Android**, the `RECORD_AUDIO` permission is declared in the plugin's `AndroidManifest.xml` and granted at install time (API ≥ 23 grants at runtime automatically).

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
    aInL inch 1    ; spin channel 1 = NativeAudioInput channel 0
    aInR inch 2    ; spin channel 2 = NativeAudioInput channel 1
    outs aInL, aInR
endin
</CsInstruments>
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

AAudio maps the callback directly to hardware DMA without mixing through AudioFlinger, giving ~4 ms round-trip latency on supported devices (Pixel 4a and later). Falls back gracefully to Unity's Microphone API if the device does not support API 26 or if exclusive mode is unavailable.

#### macOS — CoreAudio / AUHAL ####

Uses an Audio Unit HAL Output component in input-only mode. Supports any Core Audio device visible in macOS Audio MIDI Setup, including USB audio interfaces with multiple input channels. Set `ksmps` to a divisor of the hardware buffer size for minimum jitter.

#### iOS / visionOS — AudioUnit RemoteIO ####

Wraps `AudioUnit` with `kAudioOutputUnitProperty_EnableIO` on the input bus. AVAudioSession is configured for `PlayAndRecord` with `DefaultToSpeaker` and `AllowBluetooth` options. The `Requested Buffer Frames` setting maps to `AVAudioSession.preferredIOBufferDuration`.

#### Windows — WASAPI ####

Exclusive mode locks the device to the requested sample rate and buffer size, bypassing the Windows audio mixer. If exclusive mode is unavailable (device already in use), the plugin falls back to shared mode automatically. `ksmps` should divide evenly into the WASAPI engine period for glitch-free operation.
