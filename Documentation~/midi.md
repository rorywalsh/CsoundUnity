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

No additional configuration is required on macOS, iOS, or visionOS. On Android, MIDI permission is requested automatically at runtime.

#### WebGL — Web MIDI API

On WebGL builds `CsoundUnityMidiInput` uses `WebGLMidiReceiver` internally, which calls `navigator.requestMIDIAccess()` to connect all available MIDI input devices and forward incoming messages to Csound's MIDI buffer.

**Browser requirements:**
- Requires **HTTPS** — `requestMIDIAccess` is blocked on plain HTTP (`localhost` works for local testing).
- Supported on **Chrome and Edge** only. Firefox and Safari do not implement the Web MIDI API.

No extra setup is needed in the inspector — attach `CsoundUnityMidiInput` exactly as on other platforms.

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

### Sending MIDI from Unity ###

You can also send MIDI messages programmatically from C# using `CsoundUnity.MidiNoteOn`, `MidiNoteOff`, and `MidiControlChange`:

```csharp
csound.MidiNoteOn(channel: 1, note: 60, velocity: 100);
csound.MidiNoteOff(channel: 1, note: 60, velocity: 0);
csound.MidiControlChange(channel: 1, controller: 7, value: 100);
```
