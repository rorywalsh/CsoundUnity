## Root Output ##

> **New in v4.0.0 — requires Unity 6+**

RootOutput is an audio path that writes Csound's output directly into Unity's main hardware mix, bypassing the AudioMixer entirely. It uses Unity 6's `RootOutputInstance` API, which runs at the end of every mix frame — after all other audio processors — and feeds samples additively into the final hardware output.

---

### When to use RootOutput ###

For a full comparison of all audio paths see [Audio Output Path](audio_output_path.md).

RootOutput is the right choice when:
- You don't need the AudioMixer (no effects chain, no snapshots, no bus routing)
- You don't need 3D spatialization on the Csound output
- You want the lowest possible latency achievable from a C# audio path
- You want to avoid having an AudioSource on your CsoundUnity GameObject

---

### Setup ###

1. Add a `CsoundUnity` component to a GameObject — **no AudioSource required**
2. In the Inspector, find the **Audio Output Path** section (below Audio Input Routes)
3. Set **Audio Path** to `RootOutput`
4. Assign a CSD file and press Play

That's all. No additional components or configuration needed.

---

### Inspector ###

When RootOutput is selected, a HelpBox confirms that the AudioMixer is bypassed and no AudioSource is required. The `Startup Delay` option (visible on IAudioGenerator) does not apply to RootOutput — audio starts as soon as Csound initialises.

> **Note:** Audio Path cannot be changed at runtime. Set it before entering Play mode.

---

### How it works ###

RootOutput hooks into Unity's mix frame via three stages:

| Stage | What happens |
|---|---|
| `EarlyProcessing` | Fills Csound's spin buffer (NativeAudioInput routes) before any other processor runs |
| `Process` | No-op — `PerformKsmps` is a P/Invoke call and cannot run in a Burst job |
| `EndProcessing` | Runs the `PerformKsmps` loop, reads spout, writes samples into the output `ChannelBuffer` |

Samples written in `EndProcessing` are **additively mixed** into Unity's main output — they do not go through any AudioMixer bus or effect.

---

### Limitations ###

- **Unity 6+ only.** On earlier versions, `RootOutput` is not available in the Audio Path dropdown. CsoundUnity falls back to `OnAudioFilterRead` automatically at compile time.
- **No 3D spatialization.** Audio is not attached to a position in the scene. If you need panning or distance attenuation, use `OnAudioFilterRead` or `IAudioGenerator` with an AudioSource.
- **No AudioMixer effects.** Reverb, EQ, compression and other mixer effects are not applied to the Csound output.
- **No per-instance volume control via AudioSource.** Manage output level inside the CSD with `0dbfs` and amplitude scaling.
- **WebGL not supported.** The `RootOutputInstance` API is not available on WebGL; CsoundUnity uses the WASM audio path there instead.

---

### Comparison with IAudioGenerator ###

Both RootOutput and IAudioGenerator are Unity 6+ paths that avoid the overhead of `OnAudioFilterRead`. The key difference:

- **IAudioGenerator** drives an `AudioSource` — audio goes through the AudioMixer graph. Useful when you need mixer routing, snapshots, or 3D audio on the Csound output.
- **RootOutput** bypasses the mixer entirely — audio is additively mixed at the hardware output stage. Lower latency, no mixer integration.

If in doubt, use **IAudioGenerator** for full mixer compatibility, and switch to **RootOutput** when you specifically need to bypass the mixer or want minimum latency.
