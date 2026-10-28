## UI Components ##

> **New in v4.0.0**

Ready-made components that bind a Unity widget to a Csound channel. You do not write `SetChannel`
or `GetChannel` anywhere: the component carries the channel.

### Wiring one up ###

1. Put the UI object **under** the GameObject that carries `CsoundUnity`. Each component finds its
   instance with `GetComponentInParent`, so nothing has to be dragged into a field. Assign the
   **Csound** field by hand only when the UI lives somewhere else in the hierarchy. Selecting the
   `CsoundUnity` first and using the menu below does this for you: the Canvas is made as its child.
2. **GameObject > CsoundUnity > UI** and pick the widget, which drops in the prefab ready to go.
   To put the component on something you have already built, use **Add Component** and type its
   name (`CsoundUnitySlider`, `CsoundUnityKnob`, and so on).
3. Type the **Channel** name, exactly as the csd declares it.

From then on the control and the channel follow each other: moving the control writes to Csound,
and a preset applied or a channel pasted moves the control.

### Where the values come from ###

This is the part worth reading once.

**The range, the skew, the increment, the combobox options and the caption are not inspector
fields.** They are read from the `<Cabbage>` widget that declares the channel. A slider bound to

```
hslider bounds(10, 10, 300, 40) channel("cutoff") range(20, 20000, 1000, 0.5, 1) text("Cutoff")
```

sweeps 20 to 20000 with that skew and that step, and shows "Cutoff", with nothing else set.

Two components can also work without a widget: **XYPad** and **RangeSlider** have a *Manual* mode
where you type both channel names and the range yourself.

### The fields they share ###

| Field | What it does |
|---|---|
| **Csound** | The instance to talk to. Found up the hierarchy when left empty. |
| **Channel** | The channel name as the csd declares it. |
| **Label Text** | Optional `Text` showing the widget caption, or the channel name. |
| **Value Text** | Optional `Text` showing the current value. |
| **Smoothing Time** | Seconds of glide on the value, 0 to 1. 0 writes it straight through. |

Every component exposes `bool IsInitialized`, true once the linked instance has finished
initialising, and most expose `ChannelValue` so your own scripts can read the value or drive it.

### The components ###

| Component | Cabbage widget | Beyond the shared fields |
|---|---|---|
| `CsoundUnitySlider` | `hslider`, `vslider` | needs a Unity `Slider`. Has Smoothing Time |
| `CsoundUnityKnob` | `rslider` | **Knob Image** to rotate, **Min Angle** and **Max Angle** (default 135 and -135), **Drag Range** in pixels for a full sweep. Has Smoothing Time |
| `CsoundUnityEncoder` | `encoder` | endless, no min or max: it accumulates and the instrument decides what that means. **Encoder Image**, **Pixels Per Step**, **Degrees Per Step**, **Clockwise** |
| `CsoundUnityButton` | `button`, `checkbox` | needs a Unity `Button`. **Mode**: *Score* sends the **Score** event string on click, *Channel* toggles the channel between 0 and 1 |
| `CsoundUnityToggle` | `checkbox` | needs a Unity `Toggle`. Sends 0 or 1 |
| `CsoundUnityDropdown` | `combobox` | needs a Unity `Dropdown`. Options come from the widget. Csound receives the **1 based** index, as Cabbage expects |
| `CsoundUnityXYPad` | `xypad` | **Mode** *Widget* or *Manual*, the **Y Channel** for Manual, the **Dot** to move, **Label X** and **Label Y**. Has Smoothing Time |
| `CsoundUnityRangeSlider` | `hrange`, `vrange` | two handles on one track, two channels. **Mode** *Widget* or *Manual*, **Max Channel** for Manual, **Absolute Min/Max** and **Default Min/Max**, **Vertical**, the two **Handle** transforms and an optional **Fill**. Has Smoothing Time |
| `CsoundUnityNSlider` | `nslider` | a number box. Drag to change it, double click to type. **Format** (`F2` by default, `F0` for integers), **Drag Axis**, **Sensitivity**, optional **Input Field** |
| `CsoundUnityLabel` | `label` | text only. Leave the channel empty for a static label |
| `CsoundUnityMeter` | `vmeter` / `hmeter` | read only: the orchestra writes, the UI shows. Needs a Unity `Slider`, forced non interactive. The widget must declare a `range()` |
| `CsoundUnityKeyboard` | `keyboard` | **MIDI Channel**, **Base Octave** (4 = middle C), **Velocity**, **Use Physical Keyboard** (A to J and W to U play, Z and X shift octave), **Octave Shift Keys**. Sends notes with `SendMidiNoteOn` and `SendMidiNoteOff`, so the csd needs a MIDI aware instrument, for example one with `massign` |
| `CsoundUnityPianoKey` | one key | goes on each on-screen key, forwards press and release to the keyboard. Set **Semitone**, 0 = C to 11 = B. The sounding note follows the keyboard's octave |

> **`rslider` is a Knob here.** Cabbage calls it a rotary slider; the component, the prefab, the
> menu entry and the sample folder all say Knob. The csd keeps Cabbage's word, Unity keeps ours.

### Prefabs ###

```
Runtime/Utilities/Components/Prefabs/
```

`CsoundUnity_HSlider`, `CsoundUnity_VSlider`, `CsoundUnity_Knob`, `CsoundUnity_Encoder`,
`CsoundUnity_Button`, `CsoundUnity_Toggle`, `CsoundUnity_Dropdown`, `CsoundUnity_XYPad`,
`CsoundUnity_Keyboard`, `CsoundUnity_Label`, `CsoundUnity_VMeter`, `CsoundUnity_HMeter`, `CsoundUnity_NSlider`,
`CsoundUnity_HRangeSlider`, `CsoundUnity_VRangeSlider`.

`CsoundUnity_SpectrumDisplay` sits in the same folder but is a different thing: it carries an
`AudioDisplay`, which draws the waveform and the spectrum of an instance in the scene. It binds to
no channel.

Import the **UI** sample from the Package Manager for scenes that use all of these.

### Building the UI from the csd ###

The CsoundUnity inspector has a **UI Layout** foldout. It needs a settings asset, which the
**Create** button next to the field makes for you, and then offers two buttons.

- **Create UI** reads the `bounds(x, y, w, h)` of every widget in the `<Cabbage>` block and lays
  out the matching prefabs inside a `CabbageUI` panel, in a Canvas under the instance. It replaces
  a panel that is already there.
- **Update UI** keeps the panel you have and only moves the widgets back to their `bounds` and
  reapplies the font scale. Use it after editing the csd.

A click reaches a widget through an **EventSystem**, so the scene gets one if it has none, with
the input module of the Input System the project uses.

The same foldout lets you swap the prefab used for each widget type, and set a **Font Scale** for
all the generated text. The result is an approximation of the Cabbage layout, meant as a starting
point rather than a finished interface.
