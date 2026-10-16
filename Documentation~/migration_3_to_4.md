## Migration guide: 3.x → 4.0 ##

This page covers all breaking changes introduced in CsoundUnity 4.0.0. New features (Timelines, Audio Input Routing, NativeAudioInput, UI components, etc.) are documented separately and require no migration work.

---

### Namespaces

In v3, all CsoundUnity types were in the global namespace. In v4 they have been moved into explicit namespaces. Add the appropriate `using` directives to your scripts:

```csharp
using Csound.Unity;                          // CsoundUnity, CsoundUnityBridge, CsoundChannelController, ...
using Csound.Unity.NativeAudioInput;         // NativeAudioInputManager, AudioInputDevice
using Csound.Unity.Timelines;                // CsoundTimelineStarter, channel/score clips
using Csound.Unity.Utilities;                // AudioSamplesUtils, MusicUtils, RemapUtils, ...
using Csound.Unity.Utilities.Components.UI; // CsoundUnitySlider, CsoundUnityButton, ...
```

**Action:** After upgrading, any script that references CsoundUnity types will show compiler errors. Add the relevant `using` directive at the top of each file to resolve them.

---

### Csound 6 → Csound 7

All native libraries have been updated to **Csound 7.0**. Csound 7 renames and removes several opcodes and API functions.

#### Opcodes deprecated or renamed in Csound 7

| Old (Csound 6) | New (Csound 7) | Notes |
|---|---|---|
| `outs` | `outs` | Deprecated but still works — produces a warning |
| `event_i` | `eventi` | Renamed (lowercase, no underscore) |

Check the [Csound 7 changelog](https://github.com/csound/csound/releases) for a full list of renamed or removed opcodes.

**Action:** Search your CSD files for deprecated opcodes. Csound 7 logs a warning per use — check the Unity Console at startup.

#### CsoundUnityBridge API changes

`CsoundUnityBridge` wraps the Csound C API, and Csound 7 removed or reshaped part of it. Twelve of
its public methods are gone:

| Gone | Instead |
|---|---|
| `StopCsound` | `CsoundUnity.Stop()` |
| `OnApplicationQuit` | `Destroy()`, described below |
| `GetParams`, `SetParams` | nothing: Csound 7 returns an opaque `OPARMS*` that cannot be read from C# |
| `TableCopyInAsync`, `TableCopyOutAsync` | `TableCopyIn`, `TableCopyOut` |
| `Cleanup`, `GetAPIVersion`, `GetOpcodeList`, `GetNamedGEN`, `GetNamedGens`, `IsNamedGEN` | nothing: the C entry point no longer exists |

Two methods are new, `Destroy` and `EnqueueMidiMessage`.

**Most projects are unaffected** — the `CsoundUnity` API itself, `SetChannel` and `GetChannel`
included, is unchanged. Only code that called `CsoundUnityBridge` directly needs this table. The
P/Invoke layer below it was never reachable from outside the package: its entry points are
`internal`, and the class holding them is now `Csound` rather than `Csound6`.

##### `OnApplicationQuit()` renamed to `Destroy()`

`CsoundUnityBridge.OnApplicationQuit()` is now `CsoundUnityBridge.Destroy()`.

The old name described one of the method's callers rather than what it does, and it reads like a
Unity message — but `CsoundUnityBridge` is a plain C# class, not a MonoBehaviour, so Unity never
called it. In v4 it is reached from `Stop()`, from `OnDisable` and from `OnDestroy` as well, so the
name was actively misleading.

```csharp
// v3
bridge.OnApplicationQuit();

// v4
bridge.Destroy();     // returns false if refused, see below
```

`Destroy()` now returns `bool`: `false` means the call was refused because it came from inside an
audio callback on that same instance — freeing there would pull the instance out from under the
callback that asked for it. If you own the reference, keep it and call again from the main thread.
A subclass that overrides this must change its signature to `public override bool Destroy()` and
return the result of `base.Destroy()`.

---

### `csoundEventString` removed from public API

In v3, some advanced users called `csoundEventString` directly via `CsoundUnityBridge`. In v4 this is an internal implementation detail. Use the public API instead:

```csharp
// v3 (direct bridge call — no longer supported)
csound.csoundEventString("i 1 0 1");

// v4
csoundUnity.SendScoreEvent("i 1 0 1");
```

---

### Inspector: sr / kr / ksmps

The inspector fields for sample rate and control rate have been redesigned:

| v3 | v4 |
|---|---|
| `Override Sr`, `Override Kr` — two separate toggles | Single **Override sample rate (sr)** toggle; kr and ksmps are derived automatically |
| sr and kr edited independently | Editing ksmps updates kr = sr / ksmps (and vice versa) |

**Action:** Re-check your sr/kr settings in the Inspector after upgrading. The values should be preserved but the layout has changed.

---

### `GetChannel` on WebGL

In v3, `GetChannel` returned a value synchronously on all platforms. In v4, WebGL uses Csound's WASM bridge which operates asynchronously. Use the callback overload:

```csharp
// v3 — synchronous (does not work on WebGL in v4)
float val = csoundUnity.GetChannel("myChannel");

// v4 — async-safe, works on all platforms including WebGL
csoundUnity.GetChannel("myChannel", val => {
    Debug.Log(val);
});
```

The synchronous overload still works on non-WebGL platforms.

---

### Audio path defaults

Nothing to do: a component carried over from 3.x runs on `OnAudioFilterRead`, which is the default
and the only path available below Unity 6.

4.0 adds two more audio paths for Unity 6 — `IAudioGenerator` and `RootOutput` — but neither is the
default. The **Audio Output Path** section of the inspector selects them per component. This matters
for anything built by script: a `CsoundUnity` or `CsoundUnityChild` added with `AddComponent` has no
serialized value and takes the default, so it too runs on `OnAudioFilterRead` unless you set the
path yourself.

Latency is the reason. `OnAudioFilterRead` performs Csound inside the callback for the block being
produced, while a generator fills a block Unity consumes on the next pass: `IAudioGenerator` is one
audio block **later**, in exchange for having no jitter. See
[IAudioGenerator](iaudiogenerator.md) and [Audio Output Path](audio_output_path.md).

---

### `mute` now means silence, not freeze

In 3.x, muting an instance also stopped Csound performing: `PerformKsmps` was skipped for as long as the mute lasted, so the score stood still and unmuting resumed from wherever it had stopped.

In 4.0, **`mute` silences the output while Csound keeps performing**. The score advances while the instance is silent, so unmuting drops back in on the beat rather than replaying from the point it was muted. Inputs keep flowing in as well — audio input routes, native audio input and clip audio all still reach Csound, so effects fed by them stay warm.

This matters most for anything time-based: with the old behaviour, muting a Timeline-driven or sequenced instance for a few seconds left it permanently behind, and the drift accumulated with every mute.

The old behaviour is still available under its own name:

```csharp
csoundUnity.mute = true;             // silent, but still in time
csoundUnity.pauseProcessing = true;  // silent, no DSP cost, score frozen (3.x mute)
```

`pauseProcessing` also gives back the CPU saving the old mute had, and unlike disabling the GameObject or calling `Stop()` it tears nothing down: the Csound instance, its state and its channel values are preserved, and resuming is immediate.

Two related fixes come with this: `mute` previously had **no effect at all** on the IAudioGenerator and RootOutput paths (only `OnAudioFilterRead` honoured it), and a muted or paused instance now publishes silence to any instance routed from it instead of leaving it repeating the last block produced.

Note that `AudioSource.mute` is a different switch and has not changed: it silences an instance's own monitoring but leaves it feeding its routed destinations at full level, and it has no effect on the RootOutput path, which does not use an AudioSource. See [Audio Input Routing](audio_input_routing.md#silencing-an-instance-which-switch-to-use).

---

### `CsoundUnityChild` no longer forces 3D audio

In 3.x, `CsoundUnityChild.Awake` overwrote three AudioSource settings on every Play:

```csharp
audioSource.velocityUpdateMode    = AudioVelocityUpdateMode.Fixed;
audioSource.spatialBlend          = 1.0f;   // always 3D, whatever you chose
audioSource.spatializePostEffects = true;
```

The inspector was therefore lying: setting a Child to 2D appeared to work, then reverted the moment you pressed Play. In 4.0 these are **defaults applied once**, and the values you set afterwards are honoured — when the component is added in the inspector, or on `Init()` for a Child built by script:

```csharp
var child = go.AddComponent<CsoundUnityChild>();
child.Init(csound, CsoundUnityChild.AudioChannels.MONO);  // applies the defaults
child.GetComponent<AudioSource>().spatialBlend = 0f;      // override afterwards
```

**What to check when upgrading.** A Child made in 3.x has whatever `spatialBlend` was serialized, and that is usually **0** — nobody set it to 1, because doing so made no difference. Those children will now play in 2D: full level, no positioning, no distance attenuation.

If a Child should be spatialised, set **Spatial Blend** to 1 on its AudioSource. Nothing else changes, and the dummy clip the component creates is unrelated to this — it is still needed so the audio engine builds a DSP node.

The upside is that 2D is now possible at all: a Child exists to expose a named channel as a Unity output, and sending that straight out without a position is a perfectly ordinary thing to want.

---

### Presets: combobox values

A combobox channel now carries the value Cabbage uses, which counts from 1, and the conversion to
Csound happens in one place inside CsoundUnity.

**If one of your csds adds 1 to a combobox channel to work around the old behaviour, remove that
compensation.** Nothing will report it: the channel simply lands one option further along than you
asked for. The `Dr B. Samplers` csds shipped with the package did compensate and are updated.

### Presets: `CurrentPreset` and `CurrentPresetName`

`CurrentPreset` returns the `CsoundUnityPreset` that has been applied, so the preset picked in the
inspector is applied again when Csound starts. The name is `CurrentPresetName`.

```csharp
// 3.x
string name = csound.CurrentPreset;

// 4.0
string name = csound.CurrentPresetName;
CsoundUnityPreset preset = csound.CurrentPreset;
```

The two can disagree: a global preset is a snapshot of the whole component rather than a
`CsoundUnityPreset`, so it leaves only a name behind.

### Presets: applying one sets values only

Applying a preset writes the channel values, and the second value of an xypad or range widget. The
channel definition — type, range, skew, increment, a combobox's option list — stays the csd's, so a
csd edited after the preset was saved keeps its own definition.

### Samples: Input System compatibility

The v4 sample scenes use the **Legacy Input Manager** (`UnityEngine.Input`). If your project uses Unity's new Input System and the samples show input-related errors, the quickest fix is:

**Project Settings → Player → Active Input Handling → Both**

This enables both input systems simultaneously. Alternatively, the **SampleInputSystemFixer** editor script (included in the package) patches imported sample scenes automatically if you prefer to keep the new Input System exclusively.

### Minimum Unity version

CsoundUnity 4.0.0 requires **Unity 2022.3 LTS** or later, which is also the floor the Asset Store accepts. Unity 6 is required for the IAudioGenerator and RootOutput audio paths.
