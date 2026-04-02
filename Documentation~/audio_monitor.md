## Audio Monitor ##

> **New in v4.0.0**

The Audio Monitor is a real-time visual display built into the CsoundUnity inspector. It is available during **Play mode** and supports multiple simultaneous views.

Enable or disable each view independently using the toggles in the **Audio Monitor** section of the inspector.

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
