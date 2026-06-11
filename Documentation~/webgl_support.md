## WebGL support ##

From version 3.5.0 experimental WebGL platform support was added.
There are some differences in the CsoundUnity API given the async context, and also some limitations.

> **Csound version:** WebGL now uses **Csound 7** (`@csound/browser 7.0.0-beta31`). The bundle is built from the upstream `develop` branch and embedded in `csound.jspre`.


### Spatialization

Unity doesn't support the OnAudioFilterRead callback on the WebGL platform, so instead of writing and reading samples in that callback, we are creating Csound instances and sending their output directly to the speakers. 
To be able to spatialize the sources like regular AudioSources, we are sending info from Unity to Csound regarding the rotation and distance from the audio listener:

- azimuth: the horizontal rotation relative to the listener
- elevation: the vertical rotation relative to the listener
- rolloff: the volume by distance from the listener

In your csd you should use the received channel values to apply spatialization, using HRTF files.
You will need to add HRTF files (with the right sample rate) to your StreamingAssets folder: see the [Read files](webgl_support.md#read-files) section below.
 
This Csound code shows how you should setup the output of your csd:
```csound
gS_HRTF_left   =           "hrtf-44100-left.dat" ; sr should match
gS_HRTF_right  =           "hrtf-44100-right.dat"
giSine ftgen       0, 0, 2^12, 10, 1

instr 1
	aSig oscili 0.8, 440, giSine ; test signal
	; -- apply binaural 3d processing --
	; azimuth (direction in the horizontal plane)
	kAz chnget "azimuth"
	; elevation (direction in the vertical plane)
	kElev chnget "elevation"
	; rolloff (volume by distance)
	kRoll chnget "rolloff"
	; apply hrtfmove2 opcode to audio source - create stereo ouput
	aLeft, aRight  hrtfmove2   aSig, kAz, kElev, gS_HRTF_left, gS_HRTF_right
	; audio to outputs
	outs        aLeft * kRoll, aRight * kRoll
endin
```

The rolloff value is evaluated using the 3D Sound Settings of the AudioSource, so you can tweak the curve there to achieve a different rolloff behaviour.

The above code will always listen for "azimuth", "elevation" and "rolloff" channels, but they're only set on the WebGL platform when running on the browser. 
So kAz, kElev and kRoll will be always 0 when running on the Unity editor.
This means you won't hear anything as the final output will be zeroed.
The CsoundUnity.Update method runs on WebGL only, and it is responsible for those channels being set.
To be able to test your sounds on the editor, you could add a Cabbage checkbox widget in your csd to toggle between the editor and the WebGL build:

`checkbox bounds(34, 30, 130, 54) channel("isWebGL") text("Is WebGL") value(1)`

so your full csd could look like this:

```csound
<Cabbage>
form caption("Binaural Test") size(400, 300), guiMode("queue"), pluginId("bin1")
checkbox bounds(34, 30, 130, 54) channel("isWebGL") text("Is WebGL") value(1) fontColour:1(0, 255, 0, 255) 
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-odac
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

giSine          ftgen       0, 0, 2^12, 10, 1
giSquare        ftgen       0, 0, 2^12, 10, 1, 0, 1, 0             
giLFOShape      ftgen       0, 0, 131072, 19, 0.5,1,180,1 ; U-shape parabola\

gS_HRTF_left   =           "hrtf-44100-left.dat"
gS_HRTF_right  =           "hrtf-44100-right.dat"

instr 1

seed 0
ifreq random 40, 600
irate random 1, 10

kAz   init 0
kElev init 0
kRoll init 1
    
; create an audio signal
krate          oscil       irate,0.2,giLFOShape            ; rate of impulses
; amplitude envelope: a repeating pulse
kEnv           loopseg     krate,0, 0,0, 0.015,1, 0.05, 0
aSig           oscili kEnv, ifreq,giSquare                            

; get the isWebGL channel once (changing it at runtime will have no effect)
iWebGL chnget "isWebGL"

// default values when it's not webGL - i.e. the editor maybe?
// remember to set "isWebGL" channel from the host!
if iWebGL == 0 then
; no binaural 3d processing
    outs aSig, aSig
else
    ; -- apply binaural 3d processing --
    ; azimuth (direction in the horizontal plane)
    kAz chnget "azimuth"
    ; elevation (direction in the vertical plane)
    kElev chnget "elevation"
    ; rolloff (volume by distance)
    kRoll chnget "rolloff"

; apply hrtfmove2 opcode to audio source - create stereo ouput
aLeft, aRight  hrtfmove2   aSig, kAz, kElev, gS_HRTF_left, gS_HRTF_right
               outs        aLeft * kRoll, aRight * kRoll            ; audio to outputs
endif

endin

</CsInstruments>
<CsScore>
i 1 0 z ; instr 1 plays forever
</CsScore>
</CsoundSynthesizer>
;original example by Iain McCurdy
; tweaked for CsoundUnity - WebGL by gb

```
In the above example there's no binaural 3d processing when isWebGL is set to false in the CsoundUnity inspector, or its checkbox widget value  is set to 0.

### Supported API methods

- `SetChannel(string channel, MYFLT value)`
- `GetChannel(string channel, Action<MYFLT> callback)`
- `InputMessage(string scoreEvent)` — send a score event (e.g. `"i 1 0 1"`)
- `SendMidiMessage(byte status, byte data1, byte data2)` — send a raw MIDI message to the Csound WASM instance

#### GetChannel

To retrieve a channel, on the WebGL platform you should use:

`CsoundUnity.GetChannel(string channel, Action<MYFLT> callback);`

because the call is async and we need to wait for the javascript Promise to complete.
It can be used in C# like this:  

```cs
using Csound.Unity;

float _testChannel;

void Update()
{
     if (!csound || !csound.IsInitialized) return;

#if UNITY_WEBGL && !UNITY_EDITOR
     csound.GetChannel("test", (value) => _testChannel = value);
#else
     _testChannel = (float)csound.GetChannel("test");
#endif
     Debug.Log($"test channel value: {_testChannel}");
}
```
  
In this way the script supports every platform, since it only executes the async GetChannel method when running on the WebGL platform (ie on the browser, when running on the editor or other platforms it uses the default CsoundUnity implementation instead).



### MIDI input

CsoundUnity supports MIDI input on WebGL via the **Web MIDI API** (`navigator.requestMIDIAccess`).

Add a `CsoundUnityMidiInput` component to the same GameObject as `CsoundUnity`. When running in a WebGL build it uses `WebGLMidiReceiver` internally, which connects all available MIDI input devices and forwards incoming messages to Csound's MIDI buffer — the same instruments that respond to MIDI on other platforms work unchanged.

**Browser limitations:**
- Requires **HTTPS** — `requestMIDIAccess` is blocked on plain HTTP.
- Supported on **Chrome and Edge** only. Firefox and Safari do not implement the Web MIDI API.
- Each incoming MIDI message can optionally be forwarded to a Unity C# method via `SendMessage`, which is useful for custom UI feedback.

```csharp
// The component handles everything automatically.
// Just attach it in the Inspector; no additional code is required.
// For custom message handling, subscribe to the MidiMessage event:
var midi = GetComponent<CsoundUnityMidiInput>();
// midi.OnMidiMessage += (b0, b1, b2) => { ... };
```

---

### Audio input

> **Simplest path (WebGL-only workflow):** Microphone input is supported out of the box if your CSD uses opcodes that grab audio input, like [`inch`](https://csound.com/docs/manual/inch.html) — there is no need to add any Unity component or use Unity's Microphone API. The Csound WASM bundle will call `getUserMedia` automatically when the instrument requests input.

`WebGLAudioInput` is an *optional* component that wraps that built-in behaviour behind the same `Open()`/`Close()` API, `IsOpen` property, and *Open On Initialized* toggle that `NativeAudioInputManager` provides on native platforms. It is only needed if you want **`CsoundUnityAudioInputRouter`** to switch automatically between native and browser input in the same scene — useful when you develop with Editor + native audio and also publish a WebGL build.

#### Setup

Add a `WebGLAudioInput` component to the same GameObject as `CsoundUnity`. Enable **Open On Initialized** in the inspector to request microphone access automatically when Csound is ready, or call `Open()` from code.

The CSD must declare `nchnls_i ≥ 1`:

```csound
<CsInstruments>
sr     = 44100
ksmps  = 128
nchnls = 2
nchnls_i = 1   ; declare at least one input channel
0dbfs  = 1

instr 1
    aIn inch 1     ; read from browser microphone
    outs aIn, aIn
endin
</CsInstruments>
```

#### ⚠️ Two-channel limit (browser restriction)

All current browsers hard-cap `getUserMedia` audio constraints at **stereo (2 channels)**, regardless of the audio interface connected to the user's machine. Requesting more channels via `channelCount` is silently clamped to 2.

This means:
- `nchnls_i = 1` or `nchnls_i = 2` work as expected.
- `nchnls_i > 2` compiles and runs, but channels 3+ will always contain silence.
- **Multi-channel audio interfaces** (USB, FireWire, Thunderbolt, etc.) are not accessible via the Web Audio API — only the system default stereo pair is exposed, regardless of the hardware capabilities.

There is no workaround within the Web Audio standard. For multi-channel input, use [NativeAudioInput](native_audio_input.md) on desktop/mobile builds.

#### Serving over HTTPS

`getUserMedia` requires a **secure context**. Always serve your WebGL build over HTTPS (or `localhost` for local testing). On plain HTTP the microphone request will fail silently and `WebGLAudioInput.IsOpen` will remain false.

---

### Native audio input: Editor vs. WebGL build

When working on a project that targets WebGL, the Unity Build Settings platform is set to **WebGL**. In this configuration `UNITY_WEBGL` is defined — even inside the Editor. `NativeAudioInputManager` detects this and routes correctly:

| Context | Component active |
|---|---|
| Editor (any build target) | `NativeAudioInputManager` (native CoreAudio / WASAPI) |
| Standalone WebGL build | `WebGLAudioInput` (`getUserMedia`) |

Use **`CsoundUnityAudioInputRouter`** to automate this without any `#if` code in your scenes. Add all three components to the same GameObject:

1. `NativeAudioInputManager` — configured for desktop/mobile
2. `WebGLAudioInput` — configured for browser
3. `CsoundUnityAudioInputRouter` — enables the correct one at runtime

`CsoundUnityAudioInputRouter` runs in `Awake` (execution order -50), enables the appropriate component for the current platform, and disables the other. The same scene therefore works in the Editor, in a standalone build, and in a WebGL build without any manual changes.

> **Editor note:** `NativeAudioInputManager` is always used in the Unity Editor, regardless of the active Build Target — including when the target is WebGL. You can therefore test native audio input from the Editor even while iterating on a WebGL build.

<a name="read-files"></a>
### Read files

On WebGL, Csound can only read from the StreamingAssets folder.
The PersistentDataPath cannot be used, because its content is compressed into the wasm binary, and we can only access it using Unity WebRequests. The absolute path could work (so letting Csound load an asset from an absolute url) but it's not implemented at the moment.

The underlying Csound wasm object has its own file system and can fetch for files and store them.
We are using that capability when Csound is created, so CsoundUnity needs to know in advance the list of the files to be loaded from the StreamingAssets folder.

#### How to create the list of files to be loaded in WebGL

First of all, place the files you want to load in the StreamingAssets folder. If you don't have any in your project, create one.
To specify the files you want to load for a specific Csound instance, go on the related CsoundUnity inspector, Settings, Csound Global Environment Folders and add a new setting pressing the + button in the bottom right corner.
Select WebGL, StreamingAssets, and eventually type a suffix.
If there are files in the chosen folder, you should see the WebGLFilesList variable (found on top of the CsoundUnity inspector) populate with the found files. You can remove the files you don't want that Csound instance to load selecting it and pressing the '-' button. 

Remember to add an environment setting for the platform you're developing with to point to the same folder you have selected above or it won't load the files when running on the editor.
