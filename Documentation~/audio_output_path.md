## Audio Output Path ##

> **New in v4.0.0** — `OnAudioFilterRead` works everywhere; `IAudioGenerator` and `RootOutput`
> require Unity 6.

Csound's audio has to reach Unity somehow, and v4 offers three routes to do it. **The default is
`OnAudioFilterRead` on every Unity version, and most projects never need to change it.**

The path is chosen per component, in the **Audio Output Path** section of the inspector below Audio
Input Routes, and cannot be changed while the scene is running.

> **Not WebGL.** All three paths go through Unity's audio engine, and Unity does not call
> `OnAudioFilterRead` on WebGL at all. There Csound sends its output to the browser directly, and
> spatialization is done by passing the listener's azimuth, elevation and distance into the
> orchestra — see [WebGL support](webgl_support.md#spatialization).

---

### The three paths ###

| | OnAudioFilterRead | IAudioGenerator | RootOutput |
|---|---|---|---|
| Unity version | all | 6+ | 6+ |
| Default | **yes** | no | no |
| AudioSource | required | required | not used |
| AudioMixer — effects, routing, snapshots | yes | yes | bypassed |
| 3D spatialization | yes | yes | no |
| `processClipAudio` | yes | no | no |
| Latency | the reference | one DSP block later | same as OnAudioFilterRead |

---

### Which one to pick ###

**OnAudioFilterRead** is the one to keep unless something pushes you off it. It is the only path
below Unity 6, the only one that supports `processClipAudio`, and it performs Csound inside the
callback for the block being produced, so its audio leaves in that same block.

**IAudioGenerator** is what Unity 6 intends for generated audio: the generator *is* the source, so
the silent carrier clip `OnAudioFilterRead` needs — and the multiplication of Csound's output by it —
are not involved at all. It costs one DSP block, with no jitter whatsoever. Pick it when that
steadiness is worth a block, and when you still want the mixer.
See [IAudioGenerator](iaudiogenerator.md).

**RootOutput** writes straight into Unity's main mix: no AudioSource, no mixer, nothing between
Csound and the hardware. Pick it for what it removes — mixer CPU, and any chance of an effect adding
latency — rather than for speed: on an empty mixer it lands on the same sample as
`OnAudioFilterRead`. See [RootOutput](root_output.md).

---

### Measured latency ###

The figure to remember is **one block**, not a number of milliseconds: in milliseconds it becomes
whatever DSP buffer the project uses.

| Path | Latency |
|---|---|
| `OnAudioFilterRead` | delivers within the current DSP block — the reference |
| `RootOutput` | same, sample-aligned |
| `IAudioGenerator` | one DSP block later |

Measured over twenty triggers per configuration, timing from the moment a control channel is set to
the sample where the resulting click appears:

| Buffer | Difference, IAudioGenerator vs OnAudioFilterRead |
|---|---|
| 1024 frames @ 48 kHz | 20.53 ms |
| 256 frames @ 44.1 kHz | 5.80 ms |

At 256 frames all twenty clicks landed at 5.80 ms — `256/44100` to the hundredth, with no spread.
`RootOutput` writes past the point a recording inside Unity can tap, so it was measured differently:
two instances fired at the same instant, clicks at different amplitudes, recorded from outside Unity.
Nineteen of twenty triggers landed on the same sample as `OnAudioFilterRead`.

#### Why bypassing the mixer buys nothing ####

In a block-based engine only stages that **buffer** add latency — stages that read block N and emit
it in block N+1. The AudioMixer is not one of those: it is a graph evaluated inside the same
callback, so sources, effects and master all resolve within the same block. Bypassing it saves CPU,
not time. `IAudioGenerator`'s extra block fits the same rule from the other side: it genuinely is a
buffering stage.

Where `RootOutput` does win is against effects that have latency of their own. With a single **Pitch
Shifter** on the `OnAudioFilterRead` source's mixer group, pitch left at 1.0 so only its latency is
in play:

| Mixer group on the OnAudioFilterRead source | Gap |
|---|---|
| none | 0 samples, 19 of 20 triggers |
| Pitch Shifter, FFT size 1024 | **1024 samples** (21.33 ms) |
| Pitch Shifter, FFT size 2048 | **2048 samples** (42.67 ms) |

The gap tracks the effect's FFT size, which is what makes it the effect's latency rather than a
coincidence, and it does not move with the DSP buffer. So the mixer costs nothing by itself and costs
exactly what its effects cost; bypassing it saves precisely that, which on an empty chain is zero.

> Measured on macOS with Unity 6000.3.10f1. Other platforms have not been measured, and mobile is
> where these numbers would move.

---

### Silencing an instance ###

`mute` and `pauseProcessing` behave identically on all three paths: `mute` silences the output while
Csound keeps performing, `pauseProcessing` silences it and stops the DSP work as well. See
[Audio Input Routing](audio_input_routing.md#silencing-an-instance-which-switch-to-use) for the full
comparison, including how each affects instances routed from this one.

`AudioSource.mute` is a different switch and is **not** path-independent: it silences the
AudioSource, so it does nothing at all on **RootOutput**, which writes to Unity's main output and
does not use one.
