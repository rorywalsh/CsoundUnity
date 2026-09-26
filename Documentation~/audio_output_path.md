## Audio Output Path ##

> **Unity 6+ feature** — on earlier versions only `OnAudioFilterRead` is available.

CsoundUnity supports three audio output paths. The active path is selected in the **Audio Output Path** section of the Inspector (below Audio Input Routes) and cannot be changed at runtime.

---

### The three paths ###

| | OnAudioFilterRead | IAudioGenerator | RootOutput |
|---|---|---|---|
| Unity version | All | 6+ | 6+ |
| AudioSource required | Yes | Yes | No |
| AudioMixer routing | Yes | Yes | No |
| 3D spatialization | Yes | Yes | No |
| Mixer effects | Yes | Yes | No |
| Processing stages before the hardware | AudioSource + Mixer | AudioSource + Mixer | none |

---

#### OnAudioFilterRead ####

The classic Unity audio path, available on all supported Unity versions. CsoundUnity attaches to an `AudioSource` and Unity calls `OnAudioFilterRead` on the audio thread each DSP block. Audio flows through the `AudioSource` → `AudioMixer` graph.

Use this path when:
- You need to support Unity versions earlier than 6
- You need `processClipAudio` (feeding an `AudioClip` into Csound's spin buffer)

---

#### IAudioGenerator (Unity 6+) ####

CsoundUnity implements Unity 6's `IAudioGenerator` interface to drive the `AudioSource` directly, rather than post-processing the buffer Unity hands it. Audio still flows through the `AudioMixer`.

Use this path when:
- You are on Unity 6+ and want AudioMixer integration (effects, snapshots, bus routing)
- You need 3D spatialization on the Csound output
- You use Audio Input Routing between multiple CsoundUnity instances

See [IAudioGenerator](iaudiogenerator.md) for setup details and limitations.

---

#### RootOutput (Unity 6+) ####

Uses Unity 6's `RootOutputInstance` API to write Csound's output directly into the main hardware mix, bypassing the AudioMixer entirely. No `AudioSource` is required, and there are no mixer stages between Csound and the hardware.

> **On latency:** all three paths have now been measured — see below. RootOutput and OnAudioFilterRead come out sample-aligned; IAudioGenerator is one DSP block behind both.

### Measured latency ###

`IAudioGenerator` costs **exactly one DSP block** more than `OnAudioFilterRead`.

| Path | Latency |
|---|---|
| `OnAudioFilterRead` | delivers within the current DSP block (the reference) |
| `RootOutput` | same as OnAudioFilterRead, sample-aligned |
| `IAudioGenerator` | one DSP block later |

`IAudioGenerator` against `OnAudioFilterRead`, twenty triggers per configuration, measuring from the
moment a control channel is set to the sample where the resulting click appears in the final mix:

| Buffer | OnAudioFilterRead | IAudioGenerator | Difference |
|---|---|---|---|
| 1024 frames @ 48 kHz (21.33 ms) | 0 blocks | 1 block | 20.53 ms |
| 256 frames @ 44.1 kHz (5.805 ms) | 0 blocks | 1 block | 5.80 ms |

In the second configuration all twenty IAudioGenerator clicks landed at 5.80 ms, which is
`256/44100` to the hundredth, with no spread at all.

`RootOutput` cannot be measured that way, because it writes past the point the recording taps. It
was measured instead by firing two instances at the same instant — one on RootOutput, one on
OnAudioFilterRead, their clicks at different amplitudes so they can be told apart — and recording
the system output from outside Unity. No shared clock is needed, and the device latency cancels
because both clicks pass through it. At 256 frames @ 48 kHz, nineteen of twenty triggers produced
two clicks landing on the same sample; the twentieth had RootOutput 4 ms early, which is less than
one block and reflects the two instances running Csound at different points of the same mix frame.

That result deserves a word of explanation, because "bypasses the mixer" sounds like it should buy
latency and it does not. In a block-based engine only stages that *buffer* add latency — stages that
read block N and emit it in block N+1. The AudioMixer is not one of those: it is a graph evaluated
inside the same callback, so sources, effects and the master are all produced within the same block.
Bypassing it saves CPU, not time. IAudioGenerator's extra block fits the same rule from the other
side: it genuinely is a buffering stage.

Where RootOutput does win is with a mixer that carries effects which have latency of their own. That
part was measured too, by repeating the same two-instance test with a single **Pitch Shifter** (an
FFT effect, pitch left at 1.0 so only its latency is in play) on the OnAudioFilterRead source's mixer
group:

| Mixer group on the OnAudioFilterRead source | Gap between the two clicks |
|---|---|
| none | 0 samples — same sample, 19 of 20 triggers |
| Pitch Shifter, FFT size 1024 | **1024 samples** (21.33 ms) |
| Pitch Shifter, FFT size 2048 | **2048 samples** (42.67 ms) |

The delay tracks the effect's own FFT size parameter, which is what makes it the effect's latency
rather than a coincidence. It does not change with the DSP buffer size, 256 frames throughout. Some
triggers come out one hop of the FFT window early — at 2048 with an overlap of 4 the measured values
were 2048, 1536 and 1024, in steps of 512 — so the figure above is the typical and worst case.

So the rule is: the mixer costs nothing in latency by itself, and costs exactly what its effects
cost. Bypassing it with RootOutput saves precisely that, which on an empty chain is zero.

The figure to remember is **one block**, not a number of milliseconds: in milliseconds it becomes
whatever DSP buffer size the project uses. `OnAudioFilterRead` performs Csound inside the callback
for the block being produced, so its audio leaves in that same block; `IAudioGenerator` produces
into a block Unity consumes on the next pass.

Measured on macOS with Unity 6000.3.10f1. Other platforms have not been measured, and mobile in
particular is where these numbers would move.

Use this path when:
- You don't need the AudioMixer (no effects chain, no snapshots, no bus routing)
- You don't need 3D spatialization
- You want to avoid an `AudioSource` on the CsoundUnity GameObject
- You want the shortest path from Csound to the hardware

See [RootOutput](root_output.md) for setup details and limitations.

---

### Choosing a path ###

```
Need Unity < 6 support?           → OnAudioFilterRead
Need processClipAudio?            → OnAudioFilterRead
Need 3D spatialization?           → OnAudioFilterRead or IAudioGenerator
Need AudioMixer effects/routing?  → OnAudioFilterRead or IAudioGenerator
No AudioSource, no mixer?         → RootOutput
```

**IAudioGenerator** is the default on Unity 6+ and covers most use cases. It is the path Unity 6
actually intends for generated audio: the generator *is* the source, so none of the machinery
`OnAudioFilterRead` needs — a silent carrier clip for the `AudioSource` to play, and the
multiplication of Csound's output by it — is involved. Its latency is one DSP block, and perfectly
steady.

Take **OnAudioFilterRead** instead when that block matters, when you need `processClipAudio`, or when
you are shipping to Unity versions before 6. It is the only path where all three are true.

Take **RootOutput** when you want Csound's output on the hardware and nothing in between: no
`AudioSource`, no mixer CPU, no chance of an effect adding latency. On an empty mixer it is not
faster than `OnAudioFilterRead` — they come out sample-aligned — so the reason to pick it is what it
removes, not speed.

---

### Silencing an instance ###

`mute` and `pauseProcessing` behave identically on all three paths: `mute` silences the output while Csound keeps performing, `pauseProcessing` silences it and stops the DSP work as well. See [Audio Input Routing](audio_input_routing.md#silencing-an-instance-which-switch-to-use) for the full comparison, including how each one affects instances routed from this one.

`AudioSource.mute` is a different switch and is **not** path-independent: it silences the AudioSource, so it has no effect at all on **RootOutput**, which writes to Unity's main output and does not use one.

---

### NativeAudioOutput ###

> **TODO** — coming soon.

A fourth path — **NativeAudioOutputManager** — will bypass Unity's audio engine entirely, writing Csound's output directly to the hardware via CoreAudio / WASAPI, targeting sub-5 ms round-trip latency. It will coexist with the paths above: any `AudioPath` value can be used alongside NativeAudioOutput.
