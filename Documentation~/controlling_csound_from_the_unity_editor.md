## Controlling Csound from the Unity Editor

Once you have loaded a csound file into your CsoundUnity component, you are able to control parameters of your Csound code using GUI widgets generated in the inspector. 

Each of these graphical components corrisponds with a Csound channel that can be used in the csound orchestra section to control various parameters of your sound. To reveal a channel in the editor, you must write [**Cabbage widget**](https://cabbageaudio.com/docs/cabbage_syntax/) code between ```<Cabbage> </Cabbage>``` xml tags at the top of your **Csound** files (.csd).

<img src="images/controlChannels.png" alt="Add CsoundUnity"/>

```csound
<Cabbage>
form caption("SimpleFreq")
hslider channel("freq1"), text("Frequency Slider"), range(0, 10000, 0)
button channel("trigger"), text("Push me")
checkbox channel("mute")
</Cabbage>
```



The block above is an example of a Cabbage GUI section that will create graphical elements for 3 channels: a slider, a button and a checkbox(toggle). Notice that each graphical element should be on its own line and that starts with the controller type followed by  other identifiers which describe attributes of the graphical element such as *channel()*, *text()*, and *range()*.  

* Each control MUST specify a channel using the **channel()** identifier.  
* The **range()** identifier must be used if the controller type is a slider.  
* The **text()** identifier displays a label beside a control and can be useful to give a control context to a user. If no **text()** is provided, the **channel()** name will be used as the control's label. 
* The **caption()** identifier can be used in with the *form* element and is used to display some simple help text to the user.


**CsoundUnity** supports these widgets from **Cabbage**:

* rslider|hslider|vslider: horizontal sliders
* button: button with text
* checkbox: toggle box
* combobox: dropdown menu 

See [**Cabbage Widgets**](https://cabbageaudio.com/docs/cabbage_syntax/) for more information about the syntax to use.

___

## Audio Rates: sr, kr, and ksmps

The CsoundUnity Inspector exposes controls for Csound's three fundamental rate parameters:

| Parameter | Meaning |
|---|---|
| **sr** (Sample Rate) | Audio sample rate in Hz — how many audio samples Csound processes per second |
| **kr** (Control Rate) | Control sample rate in Hz — how often k-rate opcodes (envelopes, LFOs, channels) are updated |
| **ksmps** | Samples per control period: `ksmps = sr / kr` |

### How CsoundUnity sets these values

CsoundUnity passes `sr` and `ksmps` to Csound at startup using `--sample-rate` and `--ksmps` command-line options. **These override whatever `sr`, `kr`, and `ksmps` are declared inside your `.csd` file.** The CSD's own declarations are effectively ignored.

### Override Csound rates (sr / kr)

The Inspector toggle **"Override Csound rates (sr / kr)"** controls whether you set `sr` manually or let it follow the device:

**Override OFF (default)**

`sr` is set to `AudioSettings.outputSampleRate` — the actual sample rate of the audio driver on the device running the application (commonly 44100 Hz or 48000 Hz, but hardware-dependent). This ensures Csound's internal clock stays in sync with Unity's audio engine.

In this mode, `kr` is editable and `ksmps` is derived from it. By default both `sr` and `kr` start at the device rate, giving `ksmps = 1`.

**Override ON**

Both `sr` and `kr` are set manually. Use this only when you have a specific reason to run Csound at a sample rate different from the device — for example, offline rendering or a specialised instrument. Be aware that if `sr` does not match `AudioSettings.outputSampleRate`, Csound's clock will run at a different speed than Unity's, and scheduled note onsets will arrive at the wrong real time. This is especially audible in Pattern Precise mode, where entire cycles are pre-scheduled up to ~1 second ahead.

### ksmps and CPU usage

`ksmps` controls the trade-off between timing precision and CPU cost:

- **ksmps = 1** — Csound updates k-rate values (envelopes, LFOs, channels) every single sample. Maximum precision, but highest CPU usage. This is the default when override is off and `kr` has not been changed.
- **ksmps = 10–64** — A good practical range. Envelopes and channels are updated every 20–64 samples instead of every sample, reducing CPU load significantly while keeping the control rate high enough for smooth modulation.
- **Very high ksmps** — k-rate modulation becomes coarser. Envelopes may step noticeably; channels driven from Unity will update less smoothly.

**To reduce CPU usage** without changing `sr`: leave override off (or set `sr` to the device rate), then lower `kr` so that `ksmps` rises to 16–64. The ksmps field in the Inspector is an editable shortcut — entering a value automatically updates `kr = sr / ksmps`.

### Example

Device running at 48000 Hz, targeting ksmps ≈ 20:

- Override OFF
- sr: 48000 (read-only, from device)
- kr: 2400
- ksmps: 20  ← set this directly in the Inspector

Your `.csd` can still declare any `sr` / `kr` / `ksmps` — CsoundUnity's values win regardless.

___

Csound Unity is based off of the [**Cabbage**](https://cabbageaudio.com/) framework which allows for the creation of VST/AU plugins based on **Csound**. In addition to creating plugins, **Cabbage** is also a wonderful development environment for generating csound files to be used in **CsoundUnity**.  
**Cabbage** also contains many excellent examples, be aware that most are not currently supported in **CsoundUnity**, due to limited widget support. It may be possible to adapt many of the examples to work with **CsoundUnity** so please ask the [**Cabbage Forum**](https://forum.cabbageaudio.com/) for any Cabbage example that you would like to be added to the **CsoundUnity** code library.