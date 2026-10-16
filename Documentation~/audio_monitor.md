## Audio Monitor ##

> **New in v4.0.0**

The Audio Monitor is a real-time visual display built into the CsoundUnity inspector. The views draw during **Play mode** and several can be shown at once.

Enable or disable each view independently using the toggles in the **Audio Monitor** section of the inspector. The toggles can be set outside Play mode too, so a scene can be prepared before running it.

The choice of views is a preference of the editor, not of the instance: it is shared by every CsoundUnity and kept across selection changes, domain reloads and Unity restarts. Turn the waveform on once and it stays on for whatever you select next — which is what comparing two instances needs. Nothing is written to the scene.

The monitors read [`OutputBuffer`](controlling_csound_from_unity.md), so **Update Output Buffer** must be enabled in the Settings section; the inspector says so if it is not.

---

### Waveform ###

Displays the raw time-domain signal for each channel as a bar graph. A vertical slider on the right controls the amplitude zoom (1×–20×).

Channels are colour-coded: green for L (ch 0), blue for R (ch 1), and a fixed palette for additional channels.

---

### Spectrum ###

Displays the frequency content of each channel as a dB bar graph using an FFT.

- The **vertical slider** controls the dB floor (range: −20 dB to −140 dB)
- The **horizontal zoom slider** at the top narrows the displayed frequency range — drag right to zoom in on low frequencies
- dB grid lines and frequency labels are drawn automatically

---

### Spectrogram ###

A scrolling time–frequency plot using a logarithmic frequency scale. The colour scheme goes from black (silence) through blue/cyan/green/yellow to orange-red (loud).

- Shares the horizontal zoom slider with the spectrum
- The **vertical slider** controls the dB sensitivity

---

### Lissajous ###

An X–Y phase plot between two channels, useful for checking stereo width and phase correlation. Displayed as a 120×120 square.

- The **zoom slider** below the square controls the display amplitude (1×–20×)
- When `nchnls > 2`, **X Ch** and **Y Ch** sliders let you choose which channels to plot

---

### Oscilloscope ###

A triggered oscilloscope that stabilises the waveform display. Unlike the Waveform view, the oscilloscope searches for a **rising zero-crossing** on the trigger channel before drawing, so periodic signals appear stationary.

- The **zoom slider** below the square controls amplitude zoom (1×–20×)
- The **Trig Ch** dropdown to the right selects which channel is used for trigger detection
- All channels are drawn overlaid in the same square

---

### DSP Load ###

When **Measure DSP Load** is enabled on a `CsoundUnity` component, a coloured bar and percentage are shown at the top of the inspector and inside the Audio Route Graph nodes. The colour transitions from green (< 50%) through yellow to red (> 80%).

---

### In the scene: AudioDisplay ###

The views above live in the inspector, which means the editor and only the editor. **AudioDisplay**
is the same idea for a build: a component that draws a waveform or a spectrum with a
`LineRenderer`, so the picture is part of the game.

Drop it on a GameObject that has a `LineRenderer` and pick a **Mode**. The mode chooses both where
the samples come from and what is drawn:

| Mode | Reads | Draws |
|---|---|---|
| **AudioListener** | the whole scene's mix, through Unity's FFT | spectrum |
| **AudioSource** | one AudioSource, through Unity's FFT | spectrum |
| **CsoundUnityOutput** | `OutputBuffer` of a CsoundUnity instance | waveform |
| **CsoundUnityAudioChannel** | a named audio channel, written with `chnseta` | waveform |
| **RawSamples** | whatever you pass to `SetSamples` | waveform |
| **RawSamplesSpectrum** | whatever you pass to `SetSamples` | spectrum |

**CsoundUnityOutput needs Update Output Buffer** on in that instance's Settings, like the inspector
monitors.

> **AudioSource mode does not see Csound.** Unity's `GetSpectrumData` reads the clip, not what
> `OnAudioFilterRead` wrote over it, so a CsoundUnity instance shows up as its carrier clip, or as
> silence. For a per instance spectrum use **RawSamplesSpectrum**: split the buffer in your own
> `OnAudioFilterRead` and call `SetSamples` each frame.

The display box, the line width, the amplification and the response curve are all in the inspector.
The FFT runs once every **FFT Update Rate** frames, 4 by default, which is the knob to turn if a
scene draws several of these.

The prefab `CsoundUnity_SpectrumDisplay` is one already set up.

`OnDataUpdated` fires every frame with the array that was just drawn, for sending the same data on
to a shader or a second visualiser.
