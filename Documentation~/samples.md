## Samples ##

CsoundUnity includes a collection of sample scenes that demonstrate different
features and use cases. Samples are **not imported automatically** — you choose
which ones to bring into your project.

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

**Sequencers**  
Step sequencers built with CsoundUnity channels.

**Timelines**  
Integration with Unity's Timeline system: score events, channel animations, and procedural audio.

**AudioAnalysis**  
Microphone input analysis using Csound opcodes.


**Environment**  
Loading external Csound plugins and soundfont directories.

**WebGL**  
WebGL-specific setup and considerations.

---

### Input System compatibility ###

The sample scripts use the **legacy Unity Input Manager** (`UnityEngine.Input`).
This is intentional: CsoundUnity supports Unity 2020.2 and later, and adding a
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
| Input System Package (New) only | Audio runs normally; mouse/keyboard controls silently disabled |

Samples whose interaction is essential log a one-time warning so you know
why the controls are not responding.

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
