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

Several low-level API functions changed signatures in Csound 7. If you called `CsoundUnityBridge` methods directly from C# (advanced use), check the table below:

| Changed method | What changed |
|---|---|
| `csoundCreate` | Signature changed |
| `csoundCompileOrc` | Signature changed |
| `csoundCompileCSD` | Signature changed |
| `csoundEventString` | Renamed / signature changed |
| `csoundGetChannels` | Signature changed |

**Most users are unaffected** — the public `CsoundUnity` API (SetChannel, GetChannel, etc.) is unchanged. Only projects that imported and called `CsoundUnityBridge` or `CsoundCsharp` directly need to review these.

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
| `Override Sr`, `Override Kr` — two separate toggles | Single **Override Sample Rate** toggle; kr and ksmps are derived automatically |
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

### `OnAudioFilterRead` path and IAudioGenerator (Unity 6+)

On Unity 6+, the default audio path is now **IAudioGenerator** (not `OnAudioFilterRead`). Existing projects upgraded to Unity 6 will have their serialised `AudioPath` value respected — if it was previously `OnAudioFilterRead` it will remain so.

If you create a new CsoundUnity component on Unity 6, it defaults to `IAudioGenerator`. To revert to the classic path, set **Audio Path → OnAudioFilterRead** in the **Audio Output Path** section of the Inspector.

See [Audio Output Path](audio_output_path.md) for a full comparison.

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

### Samples: Input System compatibility

The v4 sample scenes use the **Legacy Input Manager** (`UnityEngine.Input`). If your project uses Unity's new Input System and the samples show input-related errors, the quickest fix is:

**Project Settings → Player → Active Input Handling → Both**

This enables both input systems simultaneously. Alternatively, the **SampleInputSystemFixer** editor script (included in the package) patches imported sample scenes automatically if you prefer to keep the new Input System exclusively.

### Minimum Unity version

CsoundUnity 4.0.0 requires **Unity 2018.2** or later (Timeline support). Unity 6 is required for the IAudioGenerator and RootOutput audio paths.
