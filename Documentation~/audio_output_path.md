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

> **On latency:** RootOutput has the fewest stages between Csound and the output, which is a structural fact rather than a measured one — the three paths have not been compared with a loopback test. Treat any latency ranking between them as unverified.

Use this path when:
- You don't need the AudioMixer (no effects chain, no snapshots, no bus routing)
- You don't need 3D spatialization
- You want to avoid an `AudioSource` on the CsoundUnity GameObject
- You want the shortest path from Csound to the hardware

See [RootOutput](root_output.md) for setup details and limitations.

---

### Choosing a path ###

```
Need Unity < 6 support?          → OnAudioFilterRead
Need processClipAudio?            → OnAudioFilterRead
Need 3D spatialization?           → OnAudioFilterRead or IAudioGenerator
Need AudioMixer effects/routing?  → OnAudioFilterRead or IAudioGenerator
Just want audio out, lowest lag?  → RootOutput
```

When in doubt, **IAudioGenerator** is the default on Unity 6+ and covers most use cases. Switch to **RootOutput** only when you specifically want to bypass the mixer.

---

### Silencing an instance ###

`mute` and `pauseProcessing` behave identically on all three paths: `mute` silences the output while Csound keeps performing, `pauseProcessing` silences it and stops the DSP work as well. See [Audio Input Routing](audio_input_routing.md#silencing-an-instance-which-switch-to-use) for the full comparison, including how each one affects instances routed from this one.

`AudioSource.mute` is a different switch and is **not** path-independent: it silences the AudioSource, so it has no effect at all on **RootOutput**, which writes to Unity's main output and does not use one.

---

### NativeAudioOutput ###

> **TODO** — coming soon.

A fourth path — **NativeAudioOutputManager** — will bypass Unity's audio engine entirely, writing Csound's output directly to the hardware via CoreAudio / WASAPI, targeting sub-5 ms round-trip latency. It will coexist with the paths above: any `AudioPath` value can be used alongside NativeAudioOutput.
