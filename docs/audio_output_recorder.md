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
| **File Name** | Without the extension. It may carry folders of its own, so `takes/test0` writes `test0` into a `takes` folder |
| **Add Channels And Time** | On, the channel count and the time are added, so two recordings never overwrite each other: `mix_6ch_20261025_095747.wav`. Off, the name is used as it is, and a second recording replaces the first |
| **Output Folder** | A base, picked with the buttons, and a folder under it that you type: see below |
| **Format** | **WAV** or **AIFF**. The extension follows the choice |
| **Bits Per Sample** | In a WAV, **32** writes floating point samples, which keep whatever the mix reached, past full scale included. **16** and **24** are the usual integer formats and clamp at full scale. An AIFF is integer at every depth, 32 included, so it always clamps |
| **Force Speaker Mode** | Ask Unity's mixer for a speaker mode whatever the output device reports. See below |
| **Speaker Mode** | The one to ask for |
| **Start On Play** | Otherwise call `StartRecording()` |
| **Stop When Full** | Write the file as soon as the recording has run its length, instead of waiting for `StopRecording()` or for the scene to end |
| **Log Report** | The table above |
| **Report Late Blocks** | Lists the blocks that arrived later than the audio they carry, with the time into the recording and Unity's dsp clock |
| **Late Block Factor** | How late counts as late, as a multiple of a block's own length. 1.5 catches a block that took half as long again as it had |

### Where the file goes

A base and a folder under it, the same shape the environment settings use, because an absolute path
is a path on one machine while these follow the player wherever it runs. Four buttons pick the base,
the white one is the base in use, and the field under them is the folder inside it:

| Base | Where |
|---|---|
| **Default** | A `Recordings` folder beside `Assets` in the editor, `persistentDataPath` in a player |
| **DataPath** | The project's `Assets`. Unity imports what lands there, so a recording becomes an AudioClip you can drop in the scene, at the price of an import every time one is written. It does not exist as such in a build |
| **Persistent Data Path** | Writable on every platform and kept across app updates. The one choice that also works in a build |
| **StreamingAssets** | Shipped with the build and read-only there, so a recording written at runtime fails and falls back. From the editor it is an ordinary folder |
| **Browse…** | Any folder on this machine. That path will not exist in a build |

The folder under the base can be more than one deep, and the file name can carry folders too, so
**DataPath** with `Recordings` and the name `test0` writes `Assets/Recordings/test0.wav` once
**Add Channels And Time** is off. The line under the field says where it will go, with **Show** to
open it.

The folder is checked by writing into it, not by looking: permissions, a read-only volume and a path
that is really a file all look like a usable folder until something is actually put in it. When the
check fails the recording still happens, into the fallback, and the console says which folder it went
to and why. A folder picked in the editor is a path on that machine, so in a build it is normal for
this to happen: `persistentDataPath` is where the file lands. The inspector also shows the last file
written, with a button to go to it.

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

### A click you can hear and cannot find ###

If something clicks while you listen but the recording is clean at that moment, the gap was not in
the audio: it was at the device. The report says so, because the recorder also times how long each
block took to come round against how long its audio lasts:

```
[AudioOutputRecorder] 2 block(s) arrived late. A click heard at one of these times is a gap at the
device, and is not in the file.
    1.18 s into the recording (dsp time 7704.27): 8.2 ms for a block worth 5.3 ms
   13.57 s into the recording (dsp time 7716.65): 8.0 ms for a block worth 5.3 ms
```

A block that takes longer than it plays leaves the device with nothing to play, and what it plays
through the hole was never handed to the recorder: that is why the click is audible and absent from
the file. The dsp time is there to line the moment up with anything else that logs that clock.

Several of these in the editor and none in a build is the editor's own work on the main thread, not
the audio. A larger DSP buffer in Project Settings is the usual remedy for the rest.

---

### Channel order ###

Unity's, which the report names: front left, front right, centre, LFE, surround left, surround
right, and for 7.1 the two rear channels after those. Stereo is left and right.
