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