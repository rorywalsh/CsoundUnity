## IAudioGenerator (Unity 6+) ##

From v4.0.0, CsoundUnity supports the **IAudioGenerator** interface introduced in Unity 6. This is the default audio path for new components on Unity 6 and above.

### What is IAudioGenerator? ###

`IAudioGenerator` is a Unity 6 API that allows a custom component to drive an `AudioSource` directly, bypassing the `OnAudioFilterRead` pipeline. CsoundUnity uses it to call `csoundPerformKsmps()` in the audio thread's `Process()` callback, writing samples directly into Unity's output buffer.

**How it compares to OnAudioFilterRead:**

- The `AudioSource` is the true audio producer. No filter chain, and no silent "carrier" clip needed
  to make the source play — the generator *is* the source
- Latency is **exactly one audio block, with no jitter**. Note that `OnAudioFilterRead` is one block
  *earlier*: it performs Csound inside the callback for the block being produced, while a generator
  fills a block Unity consumes on the next pass. So IAudioGenerator is the *predictable* path, not
  the low-latency one — see the measurements in [Audio Output Path](audio_output_path.md)
- `Configure()` runs on the control thread before every audio block, which gives a control-rate hook
  tied to the audio graph rather than to `Update()`
- `RealtimeContext.dspTime` hands you a sample-accurate DSP clock on the audio thread.
  `OnAudioFilterRead` has no equivalent: you have to read `AudioSettings.dspTime` instead

The extra block is not something CsoundUnity adds, and it is not something CsoundUnity can remove.
`CsoundRealtime.Process` writes straight into the buffer Unity hands it, sample by sample, with
nothing stored in between, and Unity's API has no field where a generator declares or trims its
latency. Measuring the DSP clock each path is handed settles where the block comes from: the
generator is given **the same clock value as RootOutput, in the same mix frame, on the same thread**,
so it is not being produced ahead of the mix. The delay is added downstream, between the generator's
output and the mixer's input. Why exactly is internal to Unity.

### Selecting the audio path ###

The **Audio Output Path** section of the Inspector (below Audio Input Routes) lets you choose between `OnAudioFilterRead`, `IAudioGenerator`, and `RootOutput`. For a full comparison see [Audio Output Path](audio_output_path.md).

The field is disabled during Play mode; changes take effect on the next initialisation.

### CsoundUnityGenerator ###

For users who prefer a leaner component, `CsoundUnityGenerator` is a standalone `MonoBehaviour` that implements only the `IAudioGenerator` interface. It has no `OnAudioFilterRead` fallback and is designed exclusively for Unity 6+.

Add it to a GameObject that already has an `AudioSource`, assign a `.csd` file, and it will compile and run Csound using the IAudioGenerator path.

### Startup delay ###

On Unity 6 with IAudioGenerator, there is a brief period between scene start and when the `AudioSource` begins requesting audio. The **Generator Startup Delay** field (default: 0.1 s) ensures Csound has time to initialise before the first audio callback arrives. Increase this value if you hear glitches at startup on slower devices.

### ksmps and buffer size ###

With IAudioGenerator, `ksmps` is passed directly to the Csound engine without rounding. The Unity audio buffer size (typically 512 samples) must be a multiple of `ksmps` for glitch-free performance. CsoundUnity logs a warning if this is not the case.

Recommended `ksmps` values for common Unity buffer sizes:

| Unity buffer | Recommended ksmps |
|---|---|
| 256 | 1, 2, 4, 8, 16, 32, 64, 128, 256 |
| 512 | 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 |
| 1024 | 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 |

### Callbacks ###

The IAudioGenerator path fires two additional callbacks per audio block, accessible via `CsoundBridgeRegistry`:

- `OnSpinFillCallback` — called before each `PerformKsmps`, used by Audio Input Routing to fill the spin buffer
- `OnKsmpsCallback` — called after each `PerformKsmps`, used to read the output buffer

These are internal hooks used by the routing system; most users will not need to interact with them directly.

### Limitations ###

#### processClipAudio not supported ####

The **Process Clip Audio** option (which feeds an `AudioSource` clip into Csound's spin buffer) is **not compatible** with the IAudioGenerator path.

In `OnAudioFilterRead` mode, Unity passes the clip's decoded samples in the `data` array of the filter callback, where CsoundUnity can stage them into the spin buffer before each `PerformKsmps`. A generator gets no such array: it is asked to *produce* a block, not to filter one.

The API does sketch a way round this. `AudioClip` declares `IAudioGenerator`, and a generator can pull another one synchronously with `RealtimeContext.Process()` — so in principle the clip could be rendered into a scratch buffer and staged into spin, in the same block. As of Unity 6000.3.10f1 that route is closed: every one of `AudioClip`'s `IAudioGenerator` members throws `NotImplementedException`. **TODO** — worth re-checking on later Unity 6 releases, since it would remove this limitation entirely.

If `processClipAudio` is enabled **and** the `AudioSource` has a clip assigned, CsoundUnity will automatically fall back to `OnAudioFilterRead` and log a warning:

```
[CsoundUnity] processClipAudio with an AudioClip assigned is not supported in
IAudioGenerator mode. Falling back to OnAudioFilterRead path ...
```

Given the above, some `processClipAudio` workflows are not available — for example,
swapping an `AudioClip` at runtime while Csound processes the samples in real
time has no IAudioGenerator equivalent.

If you only need *some* way to bring external audio into Csound, two workflows
cover most use cases:

- load samples from an `AudioClip` into a Csound ftable via the `TableLoader`
  component (under `Utilities/Components/`), then read it from the orchestra
- copy audio files to `Application.persistentDataPath` (using the
  `CopyFilesToPersistentDataPath` component) and read them with `diskin2`,
  pointing `SFDIR` at that folder

See [Loading external files](loading_external_files.md) for the full set of
approaches and their trade-offs. 


### Parts of the API CsoundUnity does not use yet ###

Listed here so the next person does not have to go digging through the Unity assemblies to find them.

- **`ProcessorInstance.Pipe`** — `SendData<T>()` and `GetAvailableData<T>()` move unmanaged structs
  between the control thread and the audio thread without allocating. This is Unity's intended channel
  for both directions. CsoundUnity still uses a `ConcurrentQueue` going down (see `CsoundCommand`) and
  `CsoundSharedBuffer` coming back up. `Update()` is a no-op in both `CsoundRealtime` and
  `CsoundControl`
- **Nested generators** — `CreateInstance` takes a `nestedFormat`, and `RealtimeContext.Process()`
  runs another `GeneratorInstance` synchronously, inside the same block. That is a zero-latency way to
  pull audio from another generator, which is exactly what a chain of routed CsoundUnity instances
  needs: today each hop costs one DSP buffer
- **`GeneratorInstance.Arguments.Speed`** — Unity passes the generator a resampling speed on every
  block, which is how it would communicate `AudioSource.pitch`. The field is `internal` to
  `UnityEngine.AudioModule`, so no package can read it: honouring pitch here is not unimplemented,
  it is not reachable. Note that `AudioSource.pitch` is inaudible on *all three* paths anyway —
  Csound generates the audio, so Unity's resampler has no source samples to stretch

One thing this path does **not** buy you, despite appearances: parallelism.
`GeneratorInstance.IRealtime` carries a `[JobProducerType]` attribute, which suggests generators
might run on job worker threads and so let several Csound instances use several cores. They do not.
Measured twice: with all three paths live in one scene, every one ran on the same audio thread; and
with three instances of a throwaway generator written to be Burst-compilable, all three still
reported a single job thread index. Generators are processed inline, one after another, exactly as
`OnAudioFilterRead` callbacks are.

What Burst is for here is the *other* kind of performance. A Burst-compiled generator is native code
with no managed allocation and no garbage collector to stall the audio thread — which matters a great
deal if your DSP is C# and not at all if, like CsoundUnity's, it is already native code behind a
handful of P/Invoke calls.

### Compatibility ###

`IAudioGenerator` features are compiled only on Unity 6 (`#if UNITY_6000_0_OR_NEWER`). On earlier Unity versions, CsoundUnity automatically falls back to `OnAudioFilterRead` regardless of the serialised `AudioPath` value.
