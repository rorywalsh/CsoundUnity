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

CsoundUnity always passes `sr` to Csound at startup via `--sample-rate`, overriding whatever `sr` is declared inside your `.csd` file.

`ksmps` and `kr` are passed only when explicitly set in the Inspector. Otherwise the values declared in your `.csd` are used.

### Override Csound rates (sr / kr)

The Inspector toggle **"Override Csound rates (sr / kr)"** controls whether you set `sr` manually or let it follow the device:

**Override OFF (default)**

`sr` is set to `AudioSettings.outputSampleRate` — the actual sample rate of the audio driver on the device running the application (commonly 44100 Hz or 48000 Hz, but hardware-dependent). This ensures Csound's internal clock stays in sync with Unity's audio engine.

`ksmps` defaults to whatever value is declared inside your `.csd` file. You can override it directly in the Inspector without enabling the Override toggle — the `ksmps` field is always editable. `kr` is derived automatically as `sr / ksmps`.

**Override ON**

`sr` is also set manually instead of following the device. Use this only when you have a specific reason to run Csound at a sample rate different from the device — for example, offline rendering or a specialised instrument. Be aware that if `sr` does not match `AudioSettings.outputSampleRate`, Csound's clock will run at a different speed than Unity's, and scheduled note onsets will arrive at the wrong real time. This is especially audible in Timeline Pattern Precise mode, where entire cycles are pre-scheduled up to ~1 second ahead.

### ksmps and CPU usage

Csound processes audio in blocks of `ksmps` samples. Unity's audio engine requests audio in fixed-size blocks (typically 1024 samples). CsoundUnity fills each Unity block by calling `PerformKsmps()` repeatedly until the block is full. With `ksmps = 1`, that means 1024 calls per audio callback; with `ksmps = 32`, only 32 calls. The per-call overhead adds up quickly at low values.

`ksmps` therefore controls the trade-off between timing precision and CPU cost:

- **ksmps = 1** — Csound updates k-rate values (envelopes, LFOs, channels) every single sample. Maximum precision, but the highest number of `PerformKsmps` calls per block and therefore the highest CPU usage.
- **ksmps = 16–64** — A good practical range. K-rate updates happen every 16–64 samples, reducing the number of calls per block significantly while keeping control resolution high enough for smooth modulation.
- **Very high ksmps** — K-rate modulation becomes coarser. Envelopes may step noticeably; channels driven from Unity will update less smoothly.

**To reduce CPU usage** without changing `sr`: leave override off (or set `sr` to the device rate), then set `ksmps` to 16–64 in the Inspector. The `kr` field updates automatically as `sr / ksmps`.

### Example

Device running at 48000 Hz, targeting ksmps ≈ 20:

- Override OFF
- sr: 48000 (read-only, from device)
- ksmps: 20  ← set this directly in the Inspector
- kr: 2400  (derived automatically)

The `ksmps` declared in your `.csd` is read and used as the default value shown in the Inspector. `sr` is always overridden by the device rate (or the manual value when Override is ON). You can change `ksmps` in the Inspector at any time.

### ksmps and IAudioGenerator (Unity 6+)

When using the **IAudioGenerator** audio path, Unity requests audio in fixed-size blocks (typically 512 samples). For glitch-free performance, the block size must be an exact multiple of `ksmps`. CsoundUnity logs a warning at startup if this is not the case.

Common safe values at a 512-sample block size: `1, 2, 4, 8, 16, 32, 64, 128, 256, 512`.

See [IAudioGenerator](iaudiogenerator.md) for more details.

___

Csound Unity is based off of the [**Cabbage**](https://cabbageaudio.com/) framework which allows for the creation of VST/AU plugins based on **Csound**. In addition to creating plugins, **Cabbage** is also a wonderful development environment for generating csound files to be used in **CsoundUnity**.  
**Cabbage** also contains many excellent examples, be aware that most are not currently supported in **CsoundUnity**, due to limited widget support. It may be possible to adapt many of the examples to work with **CsoundUnity** so please ask the [**Cabbage Forum**](https://forum.cabbageaudio.com/) for any Cabbage example that you would like to be added to the **CsoundUnity** code library.