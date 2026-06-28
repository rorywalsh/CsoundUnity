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
| Output latency (C# paths) | Good | Higher | Lowest |

---

#### OnAudioFilterRead ####

The classic Unity audio path, available on all supported Unity versions. CsoundUnity attaches to an `AudioSource` and Unity calls `OnAudioFilterRead` on the audio thread each DSP block. Audio flows through the `AudioSource` → `AudioMixer` graph.

Use this path when:
- You need to support Unity versions earlier than 6
- You need `processClipAudio` (feeding an `AudioClip` into Csound's spin buffer)

---

#### IAudioGenerator (Unity 6+) ####

CsoundUnity implements Unity 6's `IAudioGenerator` interface to drive the `AudioSource` directly. Compared to `OnAudioFilterRead`, this reduces latency and improves synchronisation with the audio graph. Audio still flows through the `AudioMixer`.

Use this path when:
- You are on Unity 6+ and want AudioMixer integration (effects, snapshots, bus routing)
- You need 3D spatialization on the Csound output
- You use Audio Input Routing between multiple CsoundUnity instances

See [IAudioGenerator](iaudiogenerator.md) for setup details and limitations.

---

#### RootOutput (Unity 6+) ####

Uses Unity 6's `RootOutputInstance` API to write Csound's output directly into the main hardware mix, bypassing the AudioMixer entirely. No `AudioSource` is required. This is the lowest-latency C# path.

Use this path when:
- You don't need the AudioMixer (no effects chain, no snapshots, no bus routing)
- You don't need 3D spatialization
- You want to avoid an `AudioSource` on the CsoundUnity GameObject
- You want minimum latency from a C# audio path

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

### NativeAudioOutput ###

> **TODO** — coming soon.

A fourth path — **NativeAudioOutputManager** — will bypass Unity's audio engine entirely, writing Csound's output directly to the hardware via CoreAudio / WASAPI, targeting sub-5 ms round-trip latency. It will coexist with the paths above: any `AudioPath` value can be used alongside NativeAudioOutput.
