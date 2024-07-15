## WebGL support ##

From version 3.5.0 experimental WebGL platform support was added.
There are some differences in the CsoundUnity API given the async context, and also some limitations.


### Spatialization

Unity doesn't support the OnAudioFilterRead callback on the WebGL platform, so instead of writing and reading samples in that callback, we are creating Csound instances and sending their output directly to the speakers. 
To be able to spatialize the sources like regular AudioSources, we are sending info to Csound regarding the rotation and distance from the audio listener:

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
So kAz, kElev and kRoll will be always 0 when running on the editor.
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
In the above example there's no binaural 3d processing when isWebGL is set to false in the CsoundUnity inspector, or its checkbox widget value should is set to 0.




### Supported API methods

Other methods will come with later versions.

- SetChannel(string channel)
- GetChannel(string channel, Action<MYFLT> callback);

#### GetChannel

To retrieve a channel, on the WebGL platform you should use:

`CsoundUnity.GetChannel(string channel, Action<MYFLT> callback);`

because the call is async and we need to wait for the javascript Promise to complete.
It can be used in C# like this:  

```cs
float _testChannel;

void Update()
{
     if (!csound || !csound.IsInitialized) return;
            
#if UNITY_WEBGL && !UNITY_EDITOR
     csound.GetChannel("test", (value) => _testChannel = value);
#else
     _testChannel = (float)csound.GetChannel("test");
#endif
     Debug.Log($"test channel value: {_testChannel";
}
```
  
In this way the script supports every platform, since it only executes the async GetChannel method when running on the WebGL platform (ie on the browser, when running on the editor or other platforms it uses the default CsoundUnity implementation instead).



### Audio input

Audio input from Unity is not supported yet (so you can't use CsoundUnity.ProcessClipAudio to gather data from an AudioClip and send it to Csound).

Microphone input on the browser is supported out of the box if you use any of the opcodes that grab audio input, like [inch](https://csound.com/docs/manual/inch.html), there's no need to use Unity's Microphone API.

<a name="read-files"></a>
### Read files

On WebGL, Csound can only read from the StreamingAssets folder.
The PersistentDataPath cannot be used, because its content is compressed into the wasm binary, and we can only access it with WebRequests. The absolute path could work (so letting Csound load an asset from an absolute url) but it's not implemented at the moment.

The underlying Csound wasm has its own file system and can fetch for files to store them there.
We are using that capability when Csound is created, so CsoundUnity needs to know in advance the list of the files to be loaded from the StreamingAssets folder.

First of all, place the files you want to load in the StreamingAssets folder. If you don't have any in your project, create one.
To specify the files you want to load for a specific Csound instance, go on the related CsoundUnity inspector, Settings, Csound Global Environment Folders and add a new setting pressing the + button in the bottom right corner.
Select WebGL, StreamingAssets, and eventually type a suffix.
If there are files in the chosen folder, you should see the WebGLFilesList on top of the CsoundUnity inspector to populate with the found files. You can remove the files you don't want Csound to load. 

Remember to add an environment setting for the platform you're developing with to point to the same folder you have selected above or it won't load the files on the editor.
