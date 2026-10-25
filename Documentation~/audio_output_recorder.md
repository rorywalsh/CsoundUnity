## Audio Output Recorder ##

> **New in v4.0.0**

`AudioOutputRecorder` writes what Unity finally plays into a WAV file, every channel of it.

Put it on the GameObject that carries the **AudioListener**. Unity hands the scene's whole mix to a
filter sitting next to one, at the width of the speaker mode and after panning, so what lands in the
file is what left for the hardware. Not only Csound: everything the scene plays is in it.

Two things it cannot see:

- **RootOutput** writes past the listener, so a CsoundUnity on that path is not in the recording.
  Capture that from the device instead.
- The **AudioListener's own volume** is applied after this point.

---

### What it is for ###

Three things, and the third is the one that saves the most time.

**Keeping what a session produced**, without a second machine or a loopback device.

**Hearing a build.** The component works in a player, where it writes into `persistentDataPath`:
the audio of a phone or a console build comes back as a file instead of as a description.

**Seeing where a sound went.** With **Log Report** on, the console gets the peak and the RMS of each
channel by name, so the question "does anything reach the surrounds" is answered by reading, not by
listening:

```
[AudioOutputRecorder] 384000 frames, 6 channels, 48000 Hz (8.00 s)
  front left      peak 0.500000 (   -6.0 dB)   rms 0.353400
  front right     peak 0.250000 (  -12.0 dB)   rms 0.176700
  centre          peak 0.000000 (   -inf dB)   rms 0.000000   SILENT
  LFE             peak 0.000000 (   -inf dB)   rms 0.000000   SILENT
  surround left   peak 0.000000 (   -inf dB)   rms 0.000000   SILENT
  surround right  peak 0.000000 (   -inf dB)   rms 0.000000   SILENT
```

---

### The inspector ###

| | |
|---|---|
| **Record Seconds** | How long to record. The buffer is allocated when recording starts: at 48 kHz a second of stereo is 384 KB, a second of 5.1 is 1.1 MB |
| **File Name** | The channel count and a timestamp are added to it, so two recordings never overwrite each other: `mix_6ch_20261025_095747.wav` |
| **Output Folder** | Left empty: a `Recordings` folder beside `Assets` in the editor, and `persistentDataPath` in a player, which is the only place a build may write |
| **Bits Per Sample** | **32** writes floating point samples, which keep whatever the mix reached, past full scale included. **16** and **24** are the usual integer formats and clamp at full scale |
| **Start On Play** | Otherwise call `StartRecording()` |
| **Stop When Full** | Write the file as soon as the recording has run its length, instead of waiting for `StopRecording()` or for the scene to end |
| **Log Report** | The table above |
| **Force Speaker Mode** | Ask Unity's mixer for a speaker mode whatever the output device reports. See below |
| **Speaker Mode** | The one to ask for |

From a script:

```csharp
var recorder = listener.GetComponent<AudioOutputRecorder>();
recorder.OnRecordingWritten += path => Debug.Log($"written: {path}");

recorder.StartRecording();
// …
recorder.StopRecording();           // recorder.LastFilePath is the file
```

---

### Recording a surround mix on a stereo machine ###

The mix runs at the narrower of two things: the speaker mode the project asks for, and what the
output device reports. So a 5.1 project on a laptop records two channels, and a surround problem
cannot be looked at on the machine that has it.

**Force Speaker Mode** asks the mixer directly. The mix then runs at that width and is reduced to
the device afterwards, and the recording is taken before the reduction, so the file has all six
channels whatever the laptop can play.

> It re-initialises Unity's audio, which invalidates clips built with `AudioClip.Create` —
> CsoundUnity rebuilds its own from `OnAudioConfigurationChanged`. Harmless where it happens, at the
> start of a recording, and a reason to leave the switch off in a game.

A wide output device is not enough by itself: a virtual device with sixteen channels is still
reported as stereo while its layout declares no surround arrangement. On macOS that is **Configure
Speakers**, in Audio MIDI Setup.

---

### Channel order ###

Unity's, which the report names: front left, front right, centre, LFE, surround left, surround
right, and for 7.1 the two rear channels after those. Stereo is left and right.
