## MIDI Input ##

> **New in v4.0.0**

CsoundUnity provides platform-agnostic MIDI input via the `CsoundUnityMidiInput` component. It selects the appropriate native backend automatically based on the target platform.

### Supported platforms ###

| Platform | Backend |
|---|---|
| macOS | CoreMIDI |
| iOS / visionOS | CoreMIDI |
| Android (API 23+) | android.media.midi |
| Windows | WinMM (short MIDI messages; SysEx not currently supported) |
| WebGL | Web MIDI API (Chrome / Edge only) |

### Setup ###

Add a `CsoundUnityMidiInput` component to the same GameObject as your `CsoundUnity` instance. The component will automatically forward MIDI messages to Csound's MIDI input buffer.

No additional configuration is required on macOS, iOS, visionOS or Windows. On Android, MIDI permission is requested automatically at runtime; on WebGL the browser asks for access the first time, and the requirements are below.

#### Several instances on Android and WebGL

On these two platforms the backend delivers messages with `UnitySendMessage`, which addresses a
**GameObject by name**. Two GameObjects with the same name are indistinguishable to it, and every
message goes to whichever one Unity finds first — so a second instance named the same as the first
receives nothing. Duplicating a GameObject is safe, since Unity names the copy "Name (1)", but this
bites as soon as the copies are renamed back or built by script.

Give each GameObject carrying a `CsoundUnityMidiInput` a distinct name. The other backends
(CoreMIDI, WinMM) address the receiver directly and are not affected.

#### WebGL — Web MIDI API

On WebGL builds `CsoundUnityMidiInput` uses `WebGLMidiReceiver` internally, which calls `navigator.requestMIDIAccess()` to connect all available MIDI input devices and forward incoming messages to Csound's MIDI buffer.

**Browser requirements:**
- Requires **HTTPS** — `requestMIDIAccess` is blocked on plain HTTP (`localhost` works for local testing).
- Supported on **Chrome and Edge** only. Firefox and Safari do not implement the Web MIDI API.

No extra setup is needed in the inspector — attach `CsoundUnityMidiInput` exactly as on other platforms.

### Choosing which MIDI sources to connect ###

On macOS, iOS, visionOS and Windows the component connects to every MIDI source it finds. Two
inspector fields narrow that down, matching substrings case-insensitively:

| Field | Effect |
|---|---|
| **Include Only Sources Containing** | when non-empty, only sources whose name contains one of these strings are connected |
| **Exclude Sources Containing** | sources whose name contains one of these strings are ignored |

Include takes priority: while it is non-empty, Exclude is not consulted. **Exclude defaults to
`IAC`**, so the macOS IAC Driver ports are left alone unless you clear the field. Include is the one
to use with a keyboard that exposes several ports — `Port 1` connects only the first.

Both are also properties, `IncludeOnlySourcesContaining` and `ExcludeSourcesContaining`. Changing
them at runtime applies at the next call to `RestartMidiInput()`.

Android and WebGL connect every available device; these two fields do not apply there.

### Using MIDI in Csound ###

Once `CsoundUnityMidiInput` is attached, MIDI data is available to Csound instruments using standard MIDI opcodes:

```csound
<CsOptions>
-+rtmidi=null -M0
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs  = 1

instr 1   ; triggered by MIDI note-on on channel 1
    iFreq cpsmidi
    iAmp  ampmidi 0.5
    aOut  oscili iAmp, iFreq
    outs  aOut, aOut
endin
</CsInstruments>
<CsScore>
f0 z
</CsScore>
```

### MIDI channel and instrument mapping ###

Csound's standard instrument-to-MIDI-channel mapping applies. Use the `-M` flag in `<CsOptions>` and assign instruments to MIDI channels with `massign`:

```csound
<CsOptions>
-+rtmidi=null -M0
</CsOptions>
<CsInstruments>
massign 1, 1   ; MIDI channel 1 → instrument 1
massign 2, 2   ; MIDI channel 2 → instrument 2
</CsInstruments>
```

### Reacting to MIDI in C# ###

While it forwards messages to Csound, `CsoundUnityMidiInput` raises four events. The MIDI channel is
1-based, as in Csound:

| Event | Arguments |
|---|---|
| `NoteOn` | channel, note, velocity |
| `NoteOff` | channel, note, velocity |
| `ControlChange` | channel, controller, value |
| `ProgramChange` | channel, program |

A Note On with velocity 0 raises `NoteOff`.

```csharp
var midi = GetComponent<CsoundUnityMidiInput>();
midi.ControlChange += (channel, controller, value) => _targetVolume = value / 127f;
```

**On macOS, iOS, visionOS and Windows the handlers run on the platform MIDI thread**, where the Unity
API is not available: store the value and read it from `Update()`. On Android and WebGL the messages
arrive on the main thread.

### Sending MIDI from Unity ###

You can also send MIDI messages programmatically from C# using `CsoundUnity.MidiNoteOn`, `MidiNoteOff`, and `MidiControlChange`:

```csharp
csound.MidiNoteOn(channel: 1, note: 60, velocity: 100);
csound.MidiNoteOff(channel: 1, note: 60, velocity: 0);
csound.MidiControlChange(channel: 1, controller: 7, value: 100);
```
