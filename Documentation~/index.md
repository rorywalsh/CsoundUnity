# Documentation v3.3.0 #

  * [How to import CsoundUnity](importing.md)
  * [Getting Started](#getting-started)
  * [Controlling Csound from Unity Scripts](#controlling-csound-from-unity-scripts)
    + [Csound's channel system](#csound-s-channel-system)
    + [Starting / Stopping Instruments](#starting---stopping-instruments)
    + [Keeping Csound performance running](#keeping-csound-performance-running)
  * [Controlling Csound from the Unity Editor](#controlling-csound-from-the-unity-editor)
  * [CsoundUnityChild](#csoundunitychild)
  * [CsoundFileWatcher](#csoundfilewatcher)
  * [Supported Platforms](#supported-platforms)


<a name="importing"></a>
## How to import CsoundUnity ##

**From version 3.0 CsoundUnity is in the form of a Unity Package**. See the [Unity Manual](https://docs.unity3d.com/Manual/PackagesList.html) for more information.  

You should use the **Unity Package Manager** to import the CsoundUnity package in your project.  
To open the Package Manager in Unity, select **Window/PackageManager** from the top menu.

**If you have git installed**: press +, *Add package from git url...*, paste this url https://github.com/rorywalsh/CsoundUnity.git, and press Add.

**If you don’t have git**: Download the Source Code (zip or tar.gz) of the [Latest Release](https://github.com/rorywalsh/CsoundUnity/releases/latest) on your local disk, extract its content, press + in the Package Manager, *Add package from disk...*, and select the package.json inside the extracted folder.  


<a name="getting_started"></a>
## Getting Started ##

CsoundUnity is a simple component that can be added to any GameObject in a scene. To do so simple hit **AddComponent** in the inspector, then click **Audio** and add **CsoundUnity**.

<img src="images/addCsoundUnityComponent_v3.gif" alt="Add CsoundUnity"/>

CsoundUnity requires the presence of an AudioSource. If the GameObject you are trying to attach a CsoundUnity component to does not already have an AudioSource attached, one will be added automatically. 

Once a CsoundUnity component has been added to a GameObject, you will need to attach a Csound file to it. Csound files can be placed anywhere inside the Assets folder. To attach a Csound file to a CsoundUnity component, simply drag it from the Assets folder to the 'Csd Asset' field in the CsoundUnity component inspector. When your game starts, Csound will feed audio from its output buffer into the AudioSource. Any audio produced by Csound can be accessed through the AudioSource component. It works for any amount of channels. See [**CsoundUnity.ProcessBlock()**](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnity.cs#L1423) 

<img src="images/addCsoundFile_v3.gif" alt="Add Csound file"/>

<a name=controlling_csound_from_unity></a>
## Controlling Csound from Unity Scripts ##

Once you have attached a Csound file to a CsoundUnity component, you may wish to control aspects of that instrument in realtime.  
Before calling any CsoundUnity methods, one must first access the component using the **GetComponent()** method. This can be seen in the sample scripts that follow.  
One usually calls GetComponent() in your script's **Awake()** or **Start()** methods.  
You should wait for Csound to be initialised before executing your code. Once the CsoundUnity component has been accessed, any of its member methods can be called. 

See some examples below:

```cs
CsoundUnity csound;

void Start()
{
	csound = GetComponent<CsoundUnity>();        
}

void Update()
{
	if (!csound.IsInitialized) return;
	// your code
}
```

```cs
CsoundUnity csound;

IEnumerator Start()
{
	csound = GetComponent<CsoundUnity>();
	while (!csound.IsInitialized)
	{
		yield return null;
	}
	
	// your code
}

// Update is called once per frame
void Update()
{
	if (!csound.IsInitialized) return;
	// your code
}
```

```cs
CsoundUnity csound;
private bool initialized;

private void Start()
{
	csound = GetComponent<CsoundUnity>();
	csound.OnCsoundInitialized += OnCsoundInitialized;
}

private void OnCsoundInitialized()
{
	initialized = true;
	Debug.Log("Csound initialised!");
	
	// your code
}

// Update is called once per frame
void Update()
{
	if (!initialized) return;

	// your code
}
```

<a name="csound-s-channel-system"></a>
### Csound's channel system ###
 
 Csound allows data to be sent and received over its channel system. To access data in Csound, one must use the **chnget** opcode. In the following code example, we access data being sent from Unity to Csoud on a channel named *speed*. The variable kSpeed will constantly update according to the value stored on the channel named *speed*. 

<img src="http://rorywalsh.github.io/CsoundUnity/images/chnget.png" alt="chnget"/>

In order to send data from Unity to Csound we must use the [**CsoundUnity.SetChannel(string channel, MYFLT value)**](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnity.cs#L812) method. 
See the **Update()** method of the script below:

```cs
// C# code

using UnityEngine;

public class CubeController : MonoBehaviour 
{
	private CsoundUnity csoundUnity;
	private CharacterController controller;

	void Start()
	{
		csoundUnity = GetComponent<CsoundUnity>();
		controller = GetComponent<CharacterController>();
	}
	
	void Update()
	{
		transform.Rotate(0, Input.GetAxis("Horizontal") * 100, 0);
		var forward = transform.TransformDirection(Vector3.forward);
		var curSpeed = 10 * Input.GetAxis("Vertical");
		controller.Simplemove(forward * curSpeed);
		csoundUnity.SetChannel("speed", controller.velocity.magnitude / 3f);
	}
}

```

Other examples:
```cs
// C# code
if (csoundUnity)
	csoundUnity.SetChannel("BPM", BPM);
```

```c
;csd file
kBPM = abs(chnget:k("BPM"))
```

<a name="starting---stopping-instruments"></a>
### Starting / Stopping Instruments ###

You can start an instrument to play at any time using the [**CsoundUnity.SendScoreEvent(string scoreEvent)**](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnity.cs#L493) method: 

```cs
// C# code
// this will instantly play instrument #1 for 10 seconds
csoundUnity.SendScoreEvent("i1 0 10");
```

You can specify the time to wait before starting the instrument:

```cs
// C# code
// start instrument #1 after 5 seconds, with 10 seconds duration
csoundUnity.SendScoreEvent("i1 5 10");
```

You can also stop a running instrument, but only if it has been started with indefinite duration (so it will keep running until you stop it), setting the duration parameter to -1:

```cs
// C# code
// instantly start instrument #1 with an indefinite duration
csoundUnity.SendScoreEvent("i1 0 -1");
```

To stop an instrument, set its number negative:

```cs
// C# code
// instantly stop instrument #1
csoundUnity.SendScoreEvent("i-1 0 -1");
```

<a name="keeping-csound-performance-running"></a>
### Keeping Csound performance running ###

Be aware that Csound will stop its performance if all the instruments (the ones listed in the score and the ones started from Unity) have stopped playing.   
You won't be able to restart the Csound performance with the current implementation of CsoundUnity.  
Instead, if you want to keep the performance active for all the time your application is running, be sure to add one of those lines to the Csound score:

```csound
<CsScore>
;causes Csound to run for about 7000 years...
f0 z 
</CsScore>
```

```csound
<CsScore>
;causes instrument 1 to run for about 7000 years...
i1 0 z
</CsScore>
```

```csound
<CsScore>
;causes instrument 1 to run for a day
i1 0 [24*60*60]
</CsScore>
```

More information about scores here:  
[https://csound.com/docs/manual/ScoreTop.html](https://csound.com/docs/manual/ScoreTop.html)  
[https://csound.com/docs/manual/ScoreEval.html](https://csound.com/docs/manual/ScoreEval.html)

<a name=controlling_csound_from_unity_editor></a>
## Controlling Csound from the Unity Editor

You can control Csound channels using the Unity Editor while you are developing your game's sounds.  
To do so, you must provide a short <Cabbage></Cabbage> descriptor at the top of your Csound files describing the channels that are needed.  
This simple descriptor section uses a single line of code to describe each channel.  
Each line starts with the given channel's controller type and is then followed by combination of other identifiers such as *channel()*, *text()*, and *range()*.  
The following descriptor sets up 3 channel controllers. A slider, a button and a checkbox(toggle).
```csound
;csd file
<Cabbage>
form caption("SimpleFreq")
hslider channel("freq1"), text("Frequency Slider"), range(0, 10000, 0)
button channel("trigger"), text("Push me")
checkbox channel("mute")
</Cabbage>
```

Each control MUST specify a channel.  
The ***range()*** identifier must be used if the controller type is a slider.  
The ***text()*** identifier can be used to display unique text beside a control but it is not required. If it is left out, the channel() name will be used as the control's label. The caption() identifier, used with form, is used to display some simple help text to the user.

See [**Cabbage Widgets**](https://cabbageaudio.com/docs/cabbage_syntax/) for more information about the syntax to use.
CsoundUnity aims to support most of the (relevant) Widgets available in Cabbage.  
[Cabbage](https://cabbageaudio.com/) is a framework for audio software development, with it you can create wonderful VST/AU plugins based on Csound.  
And of course it is a great Csound IDE!  
You will find a lot of examples inside it: be aware that most of them are not supported at the moment in CsoundUnity, since the  Widgets support is still limited. But they're worth a try!   
Feel free to ask on the [**Cabbage Forum**](https://forum.cabbageaudio.com/) for any Cabbage example that you would like to be added to CsoundUnity as a sample.

When a Csound file which contains a valid < Cabbage > section is dragged to a CsoundUnity component, Unity will generate controls for each channel.  
These controls can be tweaked when your game is running. Each time a control is modified, its value is sent to Csound from Unity on the associated channel. In this way it works the same as the method above, only we don't have to code anything in order to test our sound.  
If you change a channel value via C# code, you will see its value updated in the editor.  
For now, CsoundUnity support only four types of controller: *slider*, *checkbox(toggle)*, *button* and *comboboxes*. 


<a name=csoundunity_child></a>
## CsoundUnityChild ##

New in version 3.0, the CsoundUnityChild component lets you read the AudioChannels found in a CsoundUnity instance. You can have as many audio channels you want in your Csd.  
You can set them with the *chnset* opcode.  
See the example below:
```csound
;csd file
<Cabbage>
form caption("Test CsoundUnityChild") 
rslider channel("gain"), range(0, 1, .4, 1, .01), text("Gain")
rslider range(0, 1, 1, 1, 0.001), channel("hrm1")
rslider range(0, 1, 0, 1, 0.001), channel("hrm2")
rslider range(0, 1, 0, 1, 0.001), channel("hrm3")
rslider range(0, 1, 0, 1, 0.001), channel("hrm4")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d 
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
ksmps = 32
nchnls = 2
0dbfs = 1

giWave1 ftgen 1, 0, 4096, 10, 1
giWave2 ftgen 1, 0, 4096, 10, 1, .5, .25, .17

;this instrument sends audio to two named channels
;this audio can be picked up by any CsoundUnityChild component..
instr ChildSounds
    a1 oscili 1, 440, giWave1
    chnset a1, "sound1"
    a2 oscili 1, 840, giWave1
    chnset a2, "sound2"
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i"ChildSounds" 0 z
</CsScore>
</CsoundSynthesizer>
```

To be able to pick those named channels, create a GameObject and add the CsoundUnityChild component. An AudioSource will be added if there is none.  
There you can choose the AudioChannels that this CsoundUnityChild will play and in which configuration.  
If AudioChannelsSetting is set to MONO, the selected AudioChannel will be played in both LEFT and RIGHT channel, at half volume, to have it perfectly centered.  
If AudioChannelsSetting is set to STEREO, the selected AudioChannels will use the respective output channel. 

<img src="images/setupCsoundUnityChild_v3.gif" alt="CsoundUnityChild"/>

**Be aware that the support for audio channels is still limited**: they're parsed from the csd file, meaning that if you're using variables the CsoundUnityEditor parser won't recognize them.   
The following example will find an audioChannel named "*Sname*", instead of its expected value of "*audioChannel_1*":  

```csound
;csd file
instr 1
kEnv madsr .1, .2, .6, .4
aOut vco2 p5, p4
inumber = 1
Sname sprintf "audioChannel_%02d", inumber
chnset aOut, Sname 
outs aOut*kEnv, aOut*kEnv
endin
```

You can also create the CsoundUnity children with code, for more advanced setups.
You can get the available audio channels from CsoundUnity with:

```cs
//C# code
var audioChannels = csoundUnity.availableAudioChannels;
```

To be able to initialize a child, you should call [CsoundUnityChildren.Init(CsoundUnity csound, AudioChannels audioChannels)](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnityChild.cs#L127), to set the reference to the CsoundUnity instance that has available audio channels, and to define the channel setup to use (MONO or STEREO).
Then you must specify the audio channel you want to use with [SetAudioChannel(int channel, int audioChannel)](https://github.com/rorywalsh/CsoundUnity/blob/7f45fd3bfffa9f3d4760b0437d38de44b04a96e9/Runtime/CsoundUnityChild.cs#L143): the channel parameter is the LEFT (0) or RIGHT (1) channel, the audioChannel parameter is the index of the *CsoundUnity.availableAudioChannel* you want to use.
See the example that follows:

```csharp
// C# code  
  
[Tooltip("The radius of the circle where the children will be placed, starting from the position of this GameObject")]
[SerializeField] private float _radius = 200f;
[Tooltip("How many meters more the sources will be audible from. This value will be summed to the radius. " +
  "It is to make sure that there will be some sound when the player is equidistant from the sources")]
[SerializeField] private float _rollofTolerance = 100f;
[Tooltip("The CsoundUnity children prefab")]
[SerializeField] private GameObject _childPrefab;

private CsoundUnity _csound;

IEnumerator Start()
{
    _csound = GetComponent<CsoundUnity>();
    while (!_csound.IsInitialized)
        yield return null;

    var n = _csound.availableAudioChannels.Count;

    for (var i = 0; i < n; i++)
    {
        var angle = i * Mathf.PI * 2 / n;
        var pos = new Vector3(Mathf.Cos(angle), 0,  Mathf.Sin(angle)) * _radius;
        var go = Instantiate(_childPrefab, this.transform);

        go.transform.position = pos;
        var child = go.AddComponent<CsoundUnityChild>();
        child.Init(_csound, CsoundUnityChild.AudioChannels.MONO);
        child.SetAudioChannel(0, i);
        child.name = _csound.availableAudioChannels[i];
        var aS = go.GetComponent<AudioSource>();
        // set doppler level to 0 to avoid artefacts when the camera moves
        aS.dopplerLevel = 0;
        aS.rolloffMode = AudioRolloffMode.Custom;
        // when the audio listener is 'radius' meters far from the audio source, there will be no sound, 
        // since the rolloff function will lower the volume accordingly to the custom curve, and at maxDistance the volume will be 0. 
        // Let's add 'rollofTolerance' meters more to have some sound when the listener is equidistant from the created sources
        aS.maxDistance = _radius + _rollofTolerance;
    }
}
```

<a name=csound_filewatcher></a>
## CsoundFileWatcher ##

From version 3.0 CsoundUnity can detect the changes made to the *csd* file while modifying it in an external editor.  
You will see in the Unity Console messages like this:

```
[CsoundFileWatcher] Updating csd: sfload.csd in GameObject: Csound
```

when this happens.
This means that you don't need to drag the Csd Asset in CsoundUnity everytime you make a change on it in an external editor.

<a name=platforms></a>
## Supported Platforms ##

CsoundUnity supports building for Windows, macOS, Android 64bit (it has successfully been tested on Oculus Quest) and iOS 64bit.   
We are planning to add WebGL support, but unfortunately Unity WebGL doesn't support AudioSource.OnAudioFilterRead callback, meaning that we are not able to use the same approach we're using for the other platforms.  
This means that adding WebGL support could take a while! 
