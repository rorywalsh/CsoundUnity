## Audio Input Routing ##

> **New in v4.0.0**

Audio Input Routing lets you feed the audio output of one `CsoundUnity` instance directly into the spin buffer of another. This allows you to chain Csound instruments together — for example, routing an oscillator bank into a reverb or effects processor — without leaving the Csound/Unity audio graph.

Routes work with both **OnAudioFilterRead** and **IAudioGenerator** audio paths.

---

### Concepts ###

Each route is defined by:

| Field | Description |
|---|---|
| **Source** | The `CsoundUnity` instance producing audio |
| **Channel** | The named audio channel on the source (e.g. `main_out_0`, `audioL`) |
| **Dest Spin Channel** | The spin buffer index on the destination instance |
| **Level** | A linear gain applied to the routed signal (0–2, default 1) |

The destination receives the mixed signal in its `<CsInstruments>` via `inch` or by reading `ain` directly:

```csound
instr FxProcessor
    ain1 inch 1    ; spin channel 0
    ain2 inch 2    ; spin channel 1
    aWet reverb ain1 + ain2, 1.8
    outs aWet, aWet
endin
```

---

### Setting up routes in the inspector ###

1. Select the **destination** GameObject (the one receiving audio)
2. Expand **Audio Input Routes** in the CsoundUnity inspector
3. Press **+** to add a route
4. Set **Source**, **Channel**, **Dest Spin Channel**, and **Level**

Use the **Mute** toggle to silence all routes on a destination without removing them — useful for debugging.

#### Routing Buffer Size ####

The **Routing Buffer Size** popup controls how many frames are pre-mixed per batch (default: 512). Larger values reduce CPU overhead at the cost of slightly increased routing latency. The size is clamped to be at least `ksmps`.

---

### Audio Route Graph ###

Open the interactive route graph via **CsoundUnity → Audio Route Graph** or the **⬡** button in the inspector. The graph shows all `CsoundUnity` instances in the active scene as nodes, with edges representing active routes.

**Interactions:**

- **Drag** a node to reposition it
- **Click an edge** to select it — an info panel appears at the bottom with editable controls (channel, spin channel, level) and a **Remove** button
- **Drag from the output port** (right circle) of a node and **drop on another node** to create a new route via a configuration popup
- **Click a node** to select its GameObject in the inspector
- **Scroll wheel** or **middle-mouse drag** to pan the canvas
- **Auto Layout** button to reset node positions
- **Delete / Backspace** to remove a selected edge

Runtime DSP load bars are displayed inside each node when **Measure DSP Load** is enabled.

---

### Cycle detection ###

CsoundUnity automatically detects routing cycles (A → B → A) and prevents them by default. The `AddAudioInputRoute` method returns an `AudioRouteResult` value:

| Result | Meaning |
|---|---|
| `Success` | Route added |
| `AlreadyExists` | An identical route already exists |
| `WouldCreateCycle` | The route would create a feedback loop |

To deliberately allow a cycle (e.g. for feedback effects), enable **Force** in the route popup or pass `forceConnection: true` in code.

---

### Latency and thread safety ###

A route always carries exactly **one DSP buffer** of latency: the source publishes each finished block, and the destination mixes the most recently published one.

This is deliberate. Unity does not define the order in which `OnAudioFilterRead` runs across AudioSources, so a destination cannot know whether its source has already produced the current block. Reading the source's live buffer would mean sometimes mixing a block that is only half written — audible as intermittent clicks, and on 32-bit ARM builds as torn samples. Publishing a completed snapshot trades a fixed, predictable delay for a signal that is always intact.

At the default 512-frame buffer this is about 11 ms per hop at 48 kHz, so a chain of three instances adds roughly 32 ms. If that matters for your patch, lower the DSP buffer size in **Project Settings → Audio → DSP Buffer Size**, which reduces the per-hop cost proportionally.

A source that is muted, paused, or whose performance has finished, publishes silence — its destinations will not repeat the last block it produced.

---

### Silencing an instance: which switch to use ###

Four different things can stop an instance from being heard, and they do **not** behave the same way once that instance is part of a routing graph.

| | Own output | Csound keeps performing | Routed destinations receive | On return |
|---|---|---|---|---|
| `AudioSource.mute` | silent¹ | yes | **the signal, at full level** | — |
| `mute` | silent | yes | silence | back in time |
| `pauseProcessing` | silent | **no** | silence | resumes where it stopped |
| `Stop()` / GameObject disabled | silent | no | silence | must be reinitialised |

¹ `AudioSource.mute` has no effect on the **RootOutput** audio path, which writes to Unity's main output and does not use an AudioSource at all.

The distinction that matters most in a chain: **`AudioSource.mute` is a monitor mute.** It stops you hearing an instance locally while it carries on feeding everything routed from it — useful for auditioning one stage of a chain. `mute` is a channel mute: the signal stops at the source, and everything downstream goes quiet with it.

`mute` keeps Csound performing, so the score advances while silenced and unmuting drops back in on the beat. Input keeps flowing in too — audio input routes, native audio input and clip audio all continue to reach Csound, so effects fed by them stay warm. Use `pauseProcessing` when you want the DSP cost gone as well: it freezes the score, so clearing it resumes from exactly the point it stopped. Unlike disabling the GameObject, nothing is torn down and resuming is immediate.

---

### Code API ###

```csharp
// Add a route from 'source' to 'dest'
var result = dest.AddAudioInputRoute(
    source,
    sourceChannelName: "main_out_0",
    destSpinChannel:   0,
    level:             1f,
    forceConnection:   false);

Debug.Log(result); // Success, AlreadyExists, or WouldCreateCycle

// Remove route at index 0
dest.RemoveAudioInputRoute(0);

// Mute / unmute all routes
dest.muteAudioInputRoutes = true;

// Check for a cycle
bool wouldCycle = dest.WouldCreateCircle(source);
```

---

### Example: reverb send ###

**Oscillators.csd** (source) exports audio on `main_out_0` and `main_out_1`:

```csound
<CsInstruments>
nchnls = 2
0dbfs  = 1

instr Osc
    aL oscili 0.3, 440
    aR oscili 0.3, 660
    chnset aL, "main_out_0"
    chnset aR, "main_out_1"
    outs aL, aR
endin
</CsInstruments>
```

**Reverb.csd** (destination) reads from spin channels 0 and 1:

```csound
<CsInstruments>
nchnls = 2
0dbfs  = 1

instr FxReverb
    aInL inch 1
    aInR inch 2
    aWetL, aWetR reverbsc aInL, aInR, 0.85, 8000
    outs aWetL, aWetR
endin
</CsInstruments>
```

In the inspector, add routes on the Reverb component:
- Source: `Oscillators`, Channel: `main_out_0`, Dest Spin Channel: `0`
- Source: `Oscillators`, Channel: `main_out_1`, Dest Spin Channel: `1`
