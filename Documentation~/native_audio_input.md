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

#### Minimum latency on Windows ####

**Every stage of the chain has to agree on one sample rate, the output included.** Windows converts
silently wherever two stages disagree, and each conversion costs a buffer. The one on the output is
the most audible, because it sits at the end of the chain, and it is the one most easily missed:
it is easy to check the microphone and forget the headphones.

Four places have to say the same number:

| what | where |
|---|---|
| the capture device's default format | Windows sound settings, the input device, Advanced |
| the playback device's default format | the same, for the device you listen on |
| **Project Settings → Audio → System Sample Rate** | Unity |
| Csound's `sr` | the csd, or the sr override in the inspector |

A mismatch on the input side costs more than the conversion. WASAPI's low-latency shared path cannot
resample, so when the rates differ the plugin falls back to a classic shared stream and the reported
latency jumps: measured on one interface, 10 ms became 22 ms for the same device.

**Use one device for both input and output when you can.** One device is one clock, so capture and
playback stay in step. Across two devices the two crystals drift apart, the ring slowly fills or
empties, and the input has to be corrected every so often — which is audible when it happens.

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
| **Device** | The machine's own list of inputs, read before play. **System default** is the first entry and the default one: it opens whichever input the operating system is set to. Naming a device pins that one, which is stored as its **position** in the list — and a position is another device on another machine, so pin one for a known setup and leave the default for a scene that travels. The **↺** beside it enumerates again for a device plugged in since |
| **Auto Channel Count** | **On by default**: ask for as many channels as the CSD reads (`nchnls_i`), limited to what the device has |
| **Channel Count** | Number of input channels to capture (1–32). Used only while Auto Channel Count is off |
| **Requested Buffer Frames** | Latency hint to the driver in frames (actual may differ; 256 is a good default) |
| **Open On Initialized** | Open automatically when CsoundUnity finishes initialising |
| **Exclusive Mode** | Windows only: WASAPI exclusive mode for minimum latency (locks the device) |
| **Force Device Sample Rate** | Apple platforms only. Off, the device keeps its own rate and the input arrives transposed when that rate is not Csound's, because nothing here resamples. On, the device is moved to the rate below while this component holds it open, and put back on Close — every other app on the machine sees the new rate too |
| **Forced Sample Rate** | Read only while Force Device Sample Rate is on. A dropdown of the rates the selected device declares, with **Follow Csound** first; the **↺** beside it enumerates again for a device plugged in since |
| **Cushion Frames** | Frames kept in reserve before Csound reads, which is how late a delivery can be without leaving a gap. It is input latency one for one, so it is the knob to turn when the input feels late. 0 is automatic: it starts at twice the Unity DSP block and grows while the ring keeps running dry, which is what a device that hands over bigger pieces than Unity consumes needs |

With both defaults left on, a scene opens the microphone the user has actually selected, with the
channel count the orchestra reads, on any machine. **The first device in the list is not the system
default** — it is only the first one enumerated, which is why a scene pinned to it so often ends up
on the wrong input on someone else's computer. The list says which one the system is on.

The device has the last word on the channel count. What it granted is in `OpenedChannelCount`, and
the console says so when it is fewer than were asked for. With a mono microphone and `nchnls_i = 2`,
only the left channel carries signal: `inch 2` reads a spin channel nothing fills.

#### Which rate the input device should be on ####

The rate runs one way down the chain: the system hands Unity the output device's rate, Unity hands it
to Csound as `sr`, and the input device is nowhere in it. It is a separate piece of hardware with a
rate of its own, and when the two differ nothing resamples: the input arrives transposed, and part of
it repeats. The console says so at every open.

So the input device is the end of the chain, not the start, and the rate to put it on is Csound's.
That is what **Follow Csound** means, and it is why it is the entry to leave selected: a project whose
**System Sample Rate** is 0 follows the system, so plugging in headphones that run at another rate
moves Unity, moves Csound, and reopens the input — with a number typed in here, the input would be
the one part of the chain left behind.

Pick an actual rate only when the device refuses Csound's, and the dropdown then only offers rates
the device declares. The one it is on right now is marked. On a device that declares none — Android,
iOS and visionOS negotiate the rate at open time instead — the field stays a plain number.

The number beside **Follow Csound** is the rate Unity is **running** at, which is the one Csound is
given — not the one in **Project Settings → Audio → System Sample Rate**. The two part company
easily: Unity re-applies the setting at every keystroke while it is being typed, each incomplete
number fails, and it falls back to another rate each time. Entering play mode applies the setting; the **↺** beside
**Sample Rate (sr)** in the CsoundUnity Settings reads the running rate again.

**Lowering the cushion.** One DSP block is often enough on hardware that captures in small pieces,
and it is the first value worth trying. Lower it and watch **Starved Blocks** in play mode: as long
as it stops climbing after the first moments, the reserve is still large enough. It can be changed
while playing.

> **Windows**: selecting the system default device needs the plugin rebuilt from the current sources.
> With an older `CsoundNativeInput.dll` the component logs a warning once and falls back to the device
> named in the list.

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

// What was actually opened, after the defaults and the device had their say
Debug.Log($"device {mgr.OpenedDeviceIndex}, {mgr.OpenedChannelCount} channels");
```

For a reading of all of this on screen while the game runs, add a **CsoundUnityNativeInputMonitor**
component: it draws state, device, channels, latency and a level meter, with the ring counters behind
a toggle, and takes a line of text to describe what the scene's input is for. The
`NativeAudioInput/NativeAudioInput` sample ships with one.

---

#### Dropouts in the Editor that a build does not have ####

Playing a scene in the Editor, the input can glitch every several seconds while the same build runs
clean. The counters tell which one you are looking at: `dsp/wall` dips below 0.9 and `dropped`
jumps, while `missing` and `short` stay at 0 — the ring was healthy and the host stopped asking for
audio.

Measured on Windows with the Profiler: the frame is `EditorLoop` with its whole cost in Self and
`GC Alloc` at 0, while `PlayerLoop` takes a millisecond or so. `EditorLoop` is the Editor's own
housekeeping and does not exist in a player, which is why the build is clean. The interval wanders
between a few seconds and a dozen, so it is not a timer and not the frame rate.

**Build the scene before believing a dropout you hear in the Editor.**

### Code API ###

```csharp
using Csound.Unity.NativeAudioInput;

var mgr = GetComponent<NativeAudioInputManager>();

// Populate the Devices list before showing a device picker
mgr.EnumerateDevices();
foreach (var dev in mgr.Devices)
    Debug.Log($"[{dev.Index}] {dev.Name}  {dev.MaxChannelCount}ch  {dev.NominalSampleRate} Hz");

// Open with the component's own settings: the system default device, Auto Channel Count's channels
mgr.Open();

// Or a specific device, still with Auto Channel Count deciding the channels
mgr.Open(deviceIndex: 2);

// Or state everything explicitly: device 0, 2 channels, 256-frame buffer
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

In shared mode the plugin asks `IAudioClient3` for an engine period of `ksmps` frames, which is where
the lowest shared-mode latency comes from. That path cannot resample, so it is only available when the
device's default format already runs at Csound's rate — see
[Minimum latency on Windows](#minimum-latency-on-windows). What the driver then grants is its own
floor and not something asking for less will move: measured on two machines, a built-in codec granted
128 frames and a USB interface 441, whatever `ksmps` asked for.


**More than two inputs needs exclusive mode.** Windows shows an interface to the shared engine
with whatever layout its sound settings hold, and for most multichannel interfaces that is a
stereo pair, no matter how many inputs the hardware has. Those further inputs are still reachable,
but only past the engine: turn **Exclusive Mode** on, and the device list will report the wider
count. In shared mode the engine's layout is all there is, and a request for more is answered with
what was granted and a console line saying so.

The device list reports the wider count only where the driver admits to it. A card that keeps its
extra inputs for ASIO alone will report the stereo pair, and there is nothing WASAPI can do about
it.