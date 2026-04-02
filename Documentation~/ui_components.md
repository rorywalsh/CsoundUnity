## UI Components ##

> **New in v4.0.0**

CsoundUnity includes a set of ready-made UI components that bind directly to Csound channels. Each component requires a reference to a `CsoundUnity` instance and a channel name.

All components are found under **Add Component → CsoundUnity → UI**.

---

### CsoundUnitySlider ###

A Unity `Slider` that maps its value to a Csound channel. Supports logarithmic/exponential mapping (skew) and stepped values (increment) as declared in the Cabbage widget definition.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Channel Name` — the Csound channel to control
- `Skew` — 1 = linear, > 1 = logarithmic, < 1 = exponential
- `Increment` — step size (0 = continuous)

```csharp
// Read the current slider value
var val = csoundUnity.GetChannel("frequency");
```

---

### CsoundUnityButton ###

A Unity `Button` that sends a score event to Csound on click.

**Inspector fields:**
- `CsoundUnity` — the target instance
- `Score Event` — the score event string to send (e.g. `i1 0 1`)

```csharp
// Equivalent programmatic call
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

A Unity `Dropdown` that maps selected index to a Csound channel value.

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

### Prefabs ###

Ready-to-use prefabs for all components are included in the `Samples~` folder. Import the **UI** sample from the Package Manager to add them to your project.
