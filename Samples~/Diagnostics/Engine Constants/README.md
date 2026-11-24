# Engine Constants

Open **EngineConstants.unity** in the Editor or build it for a native player or WebGL.
The same scene checks seven engine getters against **EngineConstantsTest.csd** and displays
one PASS/FAIL report. The rising `tick` confirms performance is progressing.

The orchestra declares sr 48000, ksmps 64, four outputs, one input and 0dbfs 1.
The host decides the actual sample rate: it must be positive and kr must equal sr / ksmps.
The input count tests the engine configuration; it does not require microphone capture.

## Native platforms and Editor

The report checks that main_out_0 through main_out_3 are published. It also reports Unity's
speaker mode. Select Quad in Project Settings > Audio to test a four-channel Unity mix.
The sample keeps your current speaker mode and device selection.

An Audio Output Recorder on the camera's AudioListener automatically records ten seconds after
Csound initializes, including when the device is a virtual sound card. It saves a float32 WAV
and logs peak and RMS for each recorded channel. In Editor the file is under the project's
Recordings folder; in a player it is under persistentDataPath. The saved path is shown on screen.
Use STOP AND SAVE or RECORD 10 SECONDS for another take.

The four engine outputs carry 220, 277, 330 and 440 Hz tones. Inspect the WAV channels and the
recorder's report to see which reached the Unity listener mix. A constants PASS does not establish
physical quad output. The listener recorder does not capture the RootOutput path or routing
performed downstream by the virtual device.

## WebGL

Click the page once to allow audio. The report checks the same engine getters and explicitly
logs that main_out buffers, Unity Audio Input Routes and Unity listener recording are unavailable.
Csound plays through the browser's audio graph; the recorder is disabled. Four engine outputs do
not guarantee four device outputs: browser/device mapping needs a separate check.

After two seconds the scene also reports Csound spout peaks, incoming carrier peaks, source clip
width and source filter width. Keep the probe component above CsoundUnity in the Inspector so it
observes the carrier before CsoundUnity multiplies it by the engine output. These checkpoints
help locate a silent channel between the engine, source filter and listener mix.

The probe's Inspector meters show the source buffer before CsoundUnity: the OAFR clip carrier
can show six full-scale channels while the final mix contains only four tones. With IAudioGenerator
that same point already contains generated audio. Compare the listener WAV and recorder levels
when comparing output paths; the probe's meters are not final-output meters.

Measured on macOS Editor with the virtual device reporting 5.1: OAFR and IAudioGenerator both
record four signals at peak 0.1 on the first four 5.1 channels (FL, FR, centre, LFE), with the two
surrounds silent. In Quad mode both paths lost signals 3 and 4 before the listener recording.
This matches Unity issue UUM-146928, where AudioSource Quad output loses channels 3 and 4.
Unity lists the fix in 6000.3.29f1 (released November 19, 2026). Our reproduction used
6000.3.10f1; verification on the patched Editor is pending. Keep the scene and orchestra unchanged
when comparing Quad on those two versions. 5.1 remains a working comparison configuration.

- [Unity issue UUM-146928](https://issuetracker.unity.com/issues/23374/audiosource-only-outputs-to-2-channels-at-runtime-when-default-speaker-mode-is-set-to-quad)
- [Unity 6000.3.29f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.29f1)

WebGL browser check, November 24, 2026 (TestWebGL14): all seven getters passed with sr 48000,
kr 750, ksmps 64, four outputs, one input, 0dbfs 1 and version 7000. The tick reached 73088,
confirming performance cycles executed. The browser logged a start-promise timeout while opening
microphone input; this does not establish whether audible device output worked. main_out buffers
were absent as expected. The false compilation flag shown in that build was corrected and verified in the rebuilt browser check below.

Rebuilt WebGL browser check, November 24, 2026 (TestWebGL14): all seven getters passed again,
this time with the browser running at sr 44100 and kr 689.0625 (44100 / 64). ksmps 64, four
outputs, one input, 0dbfs 1 and version 7000 were unchanged. The report showed compiled: True,
and the supplied startup log contained no start-promise timeout. This completes the engine-getter
and compilation-status checks on WebGL; physical quad output is a separate device-routing check.
