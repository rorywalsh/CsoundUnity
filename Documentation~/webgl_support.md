## WebGL support ##

From version 3.5.0 experimental WebGL platform support was added.
There are some differences in the CsoundUnity API given the async context, and also some limitations.

### Differences from other platforms

#### Spatialization

Unity doesn't support the OnAudioFilterRead callback on the WebGL platform, so instead of writing and reading samples in that callback, we are creating Csound instances and sending their output directly to the speakers. 
To be able to spatialize the sources like regular AudioSources, we are sending info to Csound regarding the rotation and distance from the audio listener:

- azimuth: the horizontal rotation relative to the listener
- elevation: the vertical rotation relative to the listener
- rolloff: the volume by distance from the listener

In your csd you should use the received channel values to apply spatialization, using HRTF files.
You will need to add HRTF files (with the right sample rate) to your StreamingAssets folder: we can't use the PersistentDataPath on WebGL as it's compressed in the Unity wasm binary and Csound cannot access it.
This Csound code shows how you should setup the output of your csd:
```csound
gS_HRTF_left   =           "hrtf-44100-left.dat"
gS_HRTF_right  =           "hrtf-44100-right.dat"
giSine ftgen       0, 0, 2^12, 10, 1

instr 1
	aSig oscili 0.8, 440, giSine
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

#### Supported API methods

- SetChannel(string channel)
- GetChannel(string channel, Action<MYFLT> callback);

Other methods will come with later versions.

#### Audio input

Audio input from Unity is not supported yet (so you can't use CsoundUnity.ProcessClipAudio to gather data from an AudioClip and send it to Csound).

Microphone input on the browser is supported out of the box if you use any of the opcodes that grab audio input, like [inch](https://csound.com/docs/manual/inch.html), there's no need to use Unity's Microphone API.
