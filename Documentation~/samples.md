## Samples ##

CsoundUnity includes a collection of sample scenes that demonstrate different
features and use cases. Samples are **not imported automatically** — you choose
which ones to bring into your project.

### Licences ###

Six of the sample `.csd` files are CC BY-NC-SA 4.0: they cannot go into something
you sell. The samples are made to be copied and edited, so that is easy to miss.
The full list, and what the LGPL asks of you when you ship a game that links
Csound, is in [Third party notices](third_party_notices.md).

### Importing samples ###

1. Open **Window → Package Manager**
2. In the top-left dropdown select **In Project**, then find **CsoundUnity** under **Packages - Other**
3. Click the **Samples** tab
4. Press **Import** next to the category you want — each entry imports
   an entire folder containing one or more scenes and scripts

Imported samples land in `Assets/Samples/CsoundUnity/<version>/`.

---

### Sample categories ###

**Basic**  
Core CsoundUnity features: channel communication, CsoundUnityChild, lifecycle events, and value remapping.

**UI**  
CsoundUnity built-in UI components, based on Unity's uGUI system.

**Collisions**  
Physics-driven sound using Csound instruments.

**Engines**  
Procedural vehicle engine audio simulations.

**FMSynthesis**  
FM synthesis examples.

**GranularSynthesis**  
Granular synthesis in interactive 3D scenes.

**Miscellaneous**  
Assorted demos covering a variety of CsoundUnity features.

**Presets**  
Saving and recalling CsoundUnity presets at runtime.

**Samplers**  
Audio clip playback, processing, and external audio file handling through Csound.
See [Loading external files](loading_external_files.md) for the loading mechanisms these samples illustrate.

**Sequencers**  
Step sequencers built with CsoundUnity channels.

**Timelines**  
Integration with Unity's Timeline system: score events, channel animations, and procedural audio.

**AudioAnalysis**  
Microphone input analysis using Csound opcodes.

**Native Audio Input**  
Low latency multichannel input through the native plugins, one scene per platform.
See [Native Audio Input](native_audio_input.md).

**MIDI**  
MIDI input from a hardware controller, per platform. See [MIDI](midi.md).

**Audio Routing**  
Audio sent from one instance to another through named channels. `Basic Chain` is the
minimal case, `FM Audio Chain` a guided tour over three instances and four routes.
See [Audio Input Routing](audio_input_routing.md).

**Audio Output Path**  
The three audio paths side by side. See [Audio Output Path](audio_output_path.md).

**Environment**  
Loading external Csound plugins and soundfont directories.
Demonstrates `CopyFilesToPersistentDataPath` and Environment Variables —
see [Loading external files](loading_external_files.md) and
[Environment Variables](environment_variables.md).

**WebGL**  
WebGL-specific setup and considerations.

**Doppler**  
A sound that passes by. Unity's `dopplerLevel` cannot shift what Csound synthesises, so the shift is
made in the orchestra from what [CsoundUnityDoppler](doppler.md) sends it, and the propagation delay
comes free with it. The sound is the preset synth from **Presets**, so the same flyby can be an
engine, an alarm or a wind storm, changed while it runs.

**Diagnostics**  
Scenes for looking at what the package is doing, rather than for learning what it offers.
**API Smoke Test** calls the public methods one at a time and prints what each answered, as a list of
passes and failures. **Channel Mapping** records where each Csound channel lands on the speakers,
with the [Audio Output Recorder](audio_output_recorder.md), and can ask the mixer for 5.1 on a
machine whose output is stereo. **Doppler** moves a steady tone past the listener, or the listener
past it, and plays it from five sources at once: a CsoundUnity, its children, a plain AudioSource, a
clip fed through Csound, and an orchestra making its own shift. See [Doppler](doppler.md).
Each ends in something that can be pasted into a bug report.

---

### Input System compatibility ###

The sample scripts use the **legacy Unity Input Manager** (`UnityEngine.Input`).
This is intentional: adding a
hard dependency on the new Input System package would break projects that have
not adopted it, or that use no input handling at all (audio-only, headless).

All `Input.*` calls are wrapped with:

```csharp
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
    // input code
#endif
```

| Project input setting | Result |
|---|---|
| Input Manager (Old) | All samples fully interactive |
| Both | All samples fully interactive |
| Input System Package (New) only | Audio runs normally; on-screen UI works; mouse/keyboard controls in scripts silently disabled |

Samples whose interaction is essential log a one-time warning so you know
why the controls are not responding.

On-screen UI keeps working in a new Input System project: the package swaps the EventSystem of
every imported sample scene to `InputSystemUIInputModule` as it arrives. For scenes imported
before, or after switching the project over, run
**CsoundUnity → Fix Sample Scenes for New Input System** once.

If you want to enable full interaction in a new-IS project, replace the
`#if` block with new Input System equivalents. The most common mappings:

| Legacy (`UnityEngine.Input`) | New Input System |
|---|---|
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` |
| `Input.GetMouseButtonDown(0)` | `Mouse.current.leftButton.wasPressedThisFrame` |
| `Input.GetMouseButton(0)` | `Mouse.current.leftButton.isPressed` |
| `Input.GetKey(KeyCode.X)` | `Keyboard.current.xKey.isPressed` |
| `Input.GetKeyDown(KeyCode.X)` | `Keyboard.current.xKey.wasPressedThisFrame` |
| `Input.GetAxis("Horizontal")` | `Gamepad.current?.leftStick.x.ReadValue()` |
| `Input.touchCount` | `Touchscreen.current?.touches.Count` |
