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
