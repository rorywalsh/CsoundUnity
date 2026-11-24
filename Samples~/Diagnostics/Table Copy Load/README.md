# Table Copy Load

Import **Diagnostics** from the Package Manager, open **TableCopyLoad.unity**, and press Play.
Choose a table size, read or write mode, voices, burst size and interval. **START READ TEST** or **START WRITE TEST** measures both phases with the same settings. The large button and current phase stay visible
above the scrolling settings. A countdown shows when the reference ends and copies begin. Start with one request
every 100 ms and increase the load. A burst of 16 deliberately exercises queue saturation.

The oscillator reads the table being copied. Writes repeat the exact original samples, so the
copy itself should introduce no change in waveform. Listen for clicks, gaps or crackling,
especially during the copy phase. Change the voice count between comparisons to add DSP work. Each voice plays a different
pitch (113 to 302 Hz), with amplitude divided by the square root of the voice count.
This keeps the steady-state RMS level comparable between runs. The sum of peak amplitudes
is at most 0.64 with 64 voices. The timbre changes as voices are added.
Within each comparison, baseline and copy
phases use the same voices and waveform.
Preparation and voice changes happen before a one-second warm-up.
The oscillator keeps a common phase across table choices and fades voice gains to avoid
introducing a click when the comparison settings change. Preparation has its own submission
and DSP timing figures, recorded before resetting the counters for the baseline.

The comparison answers three questions:

1. Do accepted copies finish while the oscillator is playing?
2. Does the component's measured audio work stay within the DSP block budget?
3. How much main-thread time and allocation does submitting those copies cost?

The results explain each measurement before the detailed report. A larger baseline peak can come
from a single scheduling or Editor spike. Repeat comparisons before attributing a difference to
table copies. The scene compares playback with and without queued copies. It does not measure the
synchronous API or establish that async submission is free of main-thread cost.

The report includes:

- Peak measured Csound processing time per DSP block and blocks exceeding the audio time budget.
- Accepted, completed, failed and rejected requests, plus any requests pending after a five-second drain.
- Maximum main-thread time to submit a burst and observed completion latency.
- Transferred sample payload and managed garbage collection counts.

Timing is opt-in through CsoundUnity's existing DSP measurement. Peak and over-budget counters
include every measured block, including the short statistics warm-up and final drain.
They cover the same work as DspLoad. OnAudioFilterRead measures ProcessBlock.
The Unity 6 generator paths measure the Csound work accumulated across the buffer's k-cycles.
Other Unity audio sources and device scheduling are outside these measurements.
An over-budget count identifies expensive processing; a zero count does not prove glitch-free playback.
DspLoad is smoothed. The peak counter retains individual spikes. No percentile is calculated.

Completion is polled once per rendered frame. Its latency includes that polling delay.
Burst intervals are also limited by the rendered frame rate. The payload figure counts accepted sample bytes transferred, rather than measuring managed
allocation. Writes snapshot the source into up to eight reusable buffers, grown on demand and
retained until reset or destruction. First use can still allocate; each request also creates a
small completion object. Reads allocate destination arrays owned by the caller.
GC collection counts include the whole process, including this sample's on-screen UI.

Use **Copy results and measurements** and add the selected audio path, Editor or player, audio device and what you
heard. For a device check, add this scene to the build and run the same comparison there.
Select the audio path in the CsoundUnity Inspector before Play. OnAudioFilterRead is the scene's
default; IAudioGenerator and RootOutput require Unity 6. Change DSP buffer size in project audio
settings between runs and compare the budget reported by the sample.

This scene keeps all three tables allocated and stable during the test. It measures copy load
while oscillators are running. Concurrent table replacement, mixed synchronous and asynchronous
copies, and worker end-of-score behavior need separate correctness checks.
WebGL does not implement queued table copies.

## Investigating a click at the first start

The detailed report distinguishes the source-preparation submission, the first measured copy
submission, and the slowest later submission. It includes process-wide GC collection changes
during preparation and the first submission, plus their DSP clock times.
A GC count increase is correlation and does not identify the allocation that triggered it.

To capture a click on your device:

1. Reopen the scene after updating the sample. Enable **Show detailed measurements and recording**.
2. Press **Start recording audio**, wait a moment for its buffer allocation, then start the test.
3. When the test finishes, press **Stop recording and save WAV**. Keep the WAV, the copied test
   report and the AudioOutputRecorder callback-timing report in the Console.

The recorder is disabled until requested. Starting it allocates a 90-second buffer up front,
so separate that action from starting the comparison. The saved path is shown in the panel.
The recording contains Unity's listener mix; RootOutput bypasses it.
If you hear a click that is absent from the WAV, callback delivery and device buffering are
additional possibilities. A long callback interval by itself does not prove a device underrun.
