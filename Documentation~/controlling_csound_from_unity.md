## Controlling Csound from Unity Scripts ##

Once you have attached a Csound file to a CsoundUnity component, you may wish to control its parameters in real time.

All CsoundUnity types are in the `Csound.Unity` namespace. Add `using Csound.Unity;` to the top of every script that references CsoundUnity.

Before calling any of CsoundUnity's methods, use the **GetComponent()** method to obtain a reference to the CsoundUnity instance on the same (or a parent) GameObject.

It is standard to call `GetComponent()` in Unity's **Start()** or **Awake()** method, and to check `IsInitialized` before calling any Csound API.

```csharp
using Csound.Unity;
using UnityEngine;

public class MyScript : MonoBehaviour
{
    private CsoundUnity csound;

    void Start()
    {
        csound = GetComponent<CsoundUnity>();
    }

    void Update()
    {
        if (!csound.IsInitialized) return;
        // your code
    }
}
```
<!--
```csharp
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

```csharp
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
-->
<a name="csound-s-channel-system"></a>
### Csound's channel system ###
 
 Csound allows data to be sent and received over its channel system. To access data in Csound, one should use the **chnget** opcode. In the following code example, we access data being sent from Unity to Csound on a channel named *speed*. The variable kSpeed will constantly update according to the value stored on the channel named *speed*. 
```csound
// Csound Orchestra Code

instr PLAYER_MOVE
	kSpeed chnget "speed" //Gets the "speed" channel at k rate
	aNoise buzz 0.4, 100, 3, -1
	outs aNoise*kSpeed, aNoise*kSpeed 
endin

```
In order to send data from Unity to Csound we must use the **CsoundUnity.SetChannel(string channel, MYFLT value)** method. 
See the **Update()** method of the script below:

```csharp
// C# code

using UnityEngine;
using Csound.Unity;

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
		controller.SimpleMove(forward * curSpeed);

		if (!csoundUnity.IsInitialized) return;
		csoundUnity.SetChannel("speed", controller.velocity.magnitude / 3f);
	}
}

```
<!--
Other examples:
```csound
// C# code
if (csoundUnity)
	csoundUnity.SetChannel("BPM", BPM);
```

```csound
;csd file
kBPM = abs(chnget:k("BPM"))
```
-->
### String channels ###

A channel can carry a string instead of a number, which is how a file name, a preset name or any
other piece of text reaches the orchestra. `SetStringChannel` writes one and `GetStringChannel`
reads one back.

```csharp
csoundUnity.SetStringChannel("sampleName", "kick.wav");
var current = csoundUnity.GetStringChannel("status");
```

```csound
Sname chnget "sampleName"
      chnset "ready", "status"
```

String channels are separate from the control channels the inspector lists: they are not parsed
from the `<Cabbage>` block and do not appear there.

### Reading audio out of Csound ###

Control channels carry values. To take **audio** out of an instrument, write it to a named audio
channel with `chnset` and read the blocks from C#:

```csound
instr Engine
    aSig vco2 0.3, kFreq
    chnset aSig, "engine_out"
    outs aSig, aSig
endin
```

```csharp
private AudioChannelReader _reader;
private readonly float[] _block = new float[2048];
private float _level;

void Start()
{
    _reader = GetComponent<CsoundUnity>().GetAudioChannelReader("engine_out");
}

void Update()
{
    var n = _reader.Read(_block);
    if (n <= 0) return;             // nothing new since the last call, or a block was dropped

    var sum = 0f;
    for (var i = 0; i < n; i++) sum += _block[i] * _block[i];
    _level = Mathf.Sqrt(sum / n);   // drive a meter, a shader, a camera shake
}
```

The instance's own output is available the same way, on the automatic channels `main_out_0`,
`main_out_1`, … one per output channel of the CSD.

Three things are worth knowing:

- **Each reader keeps its own position in the stream**, so a meter, a visualiser and a gameplay
  script can read the same channel and all of them see every block. Call `GetAudioChannelReader`
  once and keep the reader; `HasNewBlock` tells you whether a read would deliver anything, if you
  want to skip the work around it.
- **`Read` returns a finished block**, never one being written, which costs one DSP buffer of
  latency — the same trade described in
  [Latency and thread safety](audio_input_routing.md#latency-and-thread-safety). It neither blocks
  nor allocates, so it is safe to call from an audio callback as well as from `Update()`.
- The return value has three cases: a positive count, `0` when nothing new has been published and
  your buffer is left alone, and `-1` when the producer overtook the copy, in which case the buffer
  holds a partial block and must be discarded. Treating anything that is not positive as "nothing
  to do", as above, is enough.

To send audio from one Csound instance into another, you do not need this: use
[Audio Input Routing](audio_input_routing.md), which does the same thing inside the audio engine.

---

<a name="starting---stopping-instruments"></a>
### Starting / Stopping Instruments ###

You can start an instrument to play at any time by sending a Csound Score event to CsoundUnity using the **CsoundUnity.SendScoreEvent(string scoreEvent)** method. These events follow the usual syntax of a **[Csound Score file](http://www.csounds.com/manual/html/ScoreTop.html)**.

```csound
// C# code
// this will play instrument #1 instantly for 10 seconds
csoundUnity.SendScoreEvent("i1 0 10");
```

You can specify the time to wait before starting the instrument:

```csound
// C# code
// start instrument #1 after 5 seconds, for 10 seconds
csoundUnity.SendScoreEvent("i1 5 10");
```

You can also stop a running instrument, but only if it has been started with indefinite duration (so it will keep running until you stop it), setting the duration parameter to -1:

```csound
// C# code
// instantly start instrument #1 with an indefinite duration
csoundUnity.SendScoreEvent("i1 0 -1");
```

To stop an instrument, put a "-" in front of the number to make it negative:

```csound
// C# code
// instantly stop instrument #1
csoundUnity.SendScoreEvent("i-1 0 -1");
```

<a name="keeping-csound-performance-running"></a>
### Keeping Csound performance running ###

Be aware that Csound will stop if all the instruments (the ones listed in the score and the ones started from Unity) have stopped playing. To restart a stopped performance, call `csound.Restart()` — see [Lifecycle API](lifecycle.md).

To keep the performance active for the entire duration of your application, add one of these lines to the Csound score section in your `.csd` file:

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