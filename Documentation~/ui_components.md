## UI Components ##

> **New in v4.0.0**

CsoundUnity includes a set of ready-made UI components that bind directly to Csound channels. Each component requires a reference to a `CsoundUnity` instance and a channel name.

All components are found under **Add Component → CsoundUnity → UI**.

Every component exposes a `bool IsInitialized` property that becomes `true` once the linked `CsoundUnity` instance has finished initialising.

---

### CsoundUnitySlider ###

A Unity `Slider` that maps its value to a Csound channel. Supports logarithmic/exponential mapping (skew) and stepped values (increment) as declared in the Cabbage `hslider` / `vslider` widget definition.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the Csound channel to control
- `Skew` — 1 = linear, > 1 = logarithmic, < 1 = exponential
- `Increment` — step size (0 = continuous)

```csharp
var val = csoundUnity.GetChannel("frequency");
```

---

### CsoundUnityButton ###

A Unity `Button` that sends a score event to Csound on click.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Score Event` — the score event string to send (e.g. `i1 0 1`)

```csharp
csoundUnity.SendScoreEvent("i1 0 1");
```

---

### CsoundUnityToggle ###

A Unity `Toggle` that sends one of two values to a Csound channel depending on its state.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to control
- `On Value` / `Off Value` — values sent when toggled on or off (default: 1 / 0)

---

### CsoundUnityDropdown ###

A Unity `Dropdown` that maps the selected index to a Csound channel value.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to control
- `Values` — list of values corresponding to each dropdown option

---

### CsoundUnityXYPad ###

A 2D touch/drag pad that controls two Csound channels simultaneously (X and Y axes). Useful for filter cutoff/resonance, panning/volume, or any pair of parameters.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `X Channel` / `Y Channel` — the two Csound channels to control
- `X Range` / `Y Range` — min/max for each axis

---

### CsoundUnityKeyboard ###

A piano keyboard widget that sends MIDI note-on / note-off messages to Csound. Built from individual `CsoundUnityPianoKey` components. Corresponds to the Cabbage `keyboard` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `MIDI Channel` — MIDI channel for note events (1–16)
- `Velocity` — note-on velocity (0–127)
- `Base Octave` — lowest octave displayed

Keys send note events via `CsoundUnity.MidiNoteOn` / `MidiNoteOff`. No channel name is required — note data flows through the Csound MIDI input buffer and is consumed by standard MIDI opcodes (`cpsmidi`, `ampmidi`, etc.).

---

### CsoundUnityPianoKey ###

A single key of a piano keyboard. Normally managed by `CsoundUnityKeyboard` and not added independently. Fires `NoteOn` / `NoteOff` Unity Events on press and release.

---

### CsoundUnityLabel ###

A read-only `TextMeshPro` label that displays the current value of a Csound channel as formatted text. Corresponds to the Cabbage `label` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to read
- `Format` — C# numeric format string (e.g. `F2`, `0.00`, `P0`)

---

### CsoundUnityMeter ###

A visual level meter driven by a Csound channel value. Can represent RMS level, peak amplitude, or any scalar parameter. Corresponds to the Cabbage `meter` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to read
- `Min Value` / `Max Value` — range for the meter fill (e.g. 0–1)

---

### CsoundUnityNSlider ###

A numeric text field that lets the user type a value directly into a Csound channel. Corresponds to the Cabbage `nslider` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to control
- `Min` / `Max` — clamping range
- `Increment` — step size (0 = continuous)

---

### CsoundUnityRangeSlider ###

A dual-handle slider that controls two Csound channels simultaneously — a minimum and a maximum value. Corresponds to the Cabbage `hrange` / `vrange` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name Min` / `Channel Name Max` — the two channels to control
- `Min` / `Max` — overall range for the slider
- `Orientation` — Horizontal or Vertical

---

### CsoundUnityEncoder ###

A rotary encoder widget controlled by vertical drag or mouse scroll. Sends an incremental delta to a Csound channel on each tick. Useful for BPM, coarse tuning, or any parameter that benefits from unbounded relative control. Corresponds to the Cabbage `encoder` widget.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the channel to control
- `Min` / `Max` — output range (wraps or clamps depending on configuration)
- `Increment` — delta per drag tick

---

### Prefabs ###

Ready-to-use prefabs for all components are included in the package at:

```
Runtime/Utilities/Components/Prefabs/
```

Available prefabs: `CsoundUnity_HSlider`, `CsoundUnity_VSlider`, `CsoundUnity_Knob`,
`CsoundUnity_Button`, `CsoundUnity_Toggle`, `CsoundUnity_Dropdown`, `CsoundUnity_XYPad`,
`CsoundUnity_Keyboard`, `CsoundUnity_Label`, `CsoundUnity_NSlider`,
`CsoundUnity_HRangeSlider`, `CsoundUnity_VRangeSlider`, `CsoundUnity_Encoder`.

You can also import the **UI** sample from the Package Manager for scene examples showing these components in use.

---

### Cabbage auto-layout ###

When a `.csd` file contains a `<Cabbage>` block, the CsoundUnity inspector **Create UI** button reads the `bounds(x, y, w, h)` attribute of each widget and instantiates the matching prefab at the specified position and size inside a Canvas. This gives an approximate recreation of the original Cabbage UI layout without manual placement.
