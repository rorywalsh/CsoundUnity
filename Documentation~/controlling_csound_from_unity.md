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

## Array channels (native platforms)

Array channels were introduced in Csound 7. `i[]`, `k[]` and `S[]` channels carry one array under one
channel name. These differ from the older
`chngetk`/`chnsetk` family that operates on several scalar channel names. Numeric and UTF-8 string arrays are supported; `a[]` and PVS data are separate features. Array channels are API
only: no Cabbage widget or preset field is generated. `GetChannelList()` identifies them as
`CsoundUnityBridge.ChannelType.Array`.

The CSD can declare the channel before any instrument reads it:

```csound
giShape[] fillarray 4
chnarray "values", 3, "k", giShape

instr 1
    kValues[] chnget "values"
    chnset kValues[0], "firstValue"
endin
```

Alternatively, create it from the host with `InitializeArrayChannel`. Wait for `IsInitialized`,
then initialize once during setup. The method briefly suspends Unity audio entry while initialization
allocates; doing this during playback may produce a short silence. It returns `Busy` when called from
an audio callback or when it cannot suspend within the setup wait. A worker subclass driving its own
performance thread must stop that thread before initialization. Already initialized channels retain
their values: a different type or shape returns `ShapeMismatch` instead of silently doing nothing.

Use the package's platform sample precision in your script:

```csharp
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else // Android, iOS, visionOS; WebGL returns UnsupportedPlatform
using MYFLT = System.Single;
#endif
using Csound.Unity;

// Call during setup, after csound.IsInitialized. In this CSD it verifies the existing declaration.
var status = csound.InitializeArrayChannel("values", CsoundArrayChannelType.Control, 4);
if (status != CsoundArrayChannelStatus.Success) return;

var sent = new MYFLT[] { 1, 2, 3, 4 };
var received = new MYFLT[4];
var sizes = new int[1];

status = csound.SetArrayChannel("values", sent);
status = csound.GetArrayChannel("values", received, sizes, out var info);
// On Success: info.ElementType == Control, Dimensions == 1, ElementCount == 4, sizes[0] == 4.
// Keep and reuse the buffers in later calls rather than allocating them every frame.
```

For init-rate values use `CsoundArrayChannelType.Init`. Csound `chnget.i[]` reads at instrument init;
a later host write does not rerun an already initialized instrument. Multidimensional arrays use
one size per axis (`InitializeArrayChannel("matrix", type, 2, 3)`) and a flat managed buffer in Csound
order, with the last axis varying fastest. Zero-length axes are supported.

| API | Contract |
|---|---|
| `InitializeArrayChannel(name, type, sizes)` | Create or verify exact type and shape; never resize an initialized channel. Setup only. |
| `GetArrayChannelInfo(name, sizes, out info)` | Read element type, rank, element count and axis sizes. No channel creation. |
| `GetArrayChannel(name, destination, out length)` | Copy the whole flat array and report its current element count. |
| `GetArrayChannel(name, destination, sizes, out info)` | Copy values and matching shape under one channel lock. Prefer this for multidimensional or resizing producers. |
| `SetArrayChannel(name, source)` | Replace all numeric values in the current shape. Source length must match exactly. No creation or resizing. |
| `GetStringArrayChannel(name, destination, out length)` | Read a complete `S[]` into a reusable `string[]`. |
| `GetStringArrayChannel(name, destination, sizes, out info)` | Read UTF-8 text and matching shape as one snapshot. |
| `SetStringArrayChannel(name, source)` | Replace all string elements in the current shape; exact length required. |

Normal outcomes are returned as `CsoundArrayChannelStatus`: `Success`, `Unavailable`, `NotFound`,
`NotInitialized`, `UnsupportedType`, `BufferTooSmall`, `ShapeMismatch`, `Busy`, `UnsupportedPlatform`
or `NativeError`. Invalid managed arguments throw `ArgumentException`/`ArgumentNullException`/
`ArgumentOutOfRangeException`. Names must be nonempty, contain no null character and fit Csound's
1,024-byte UTF-8 channel-name limit.

A too-small buffer is left unchanged. Numeric and string getters still report the required length or metadata;
`GetArrayChannelInfo` reports the required rank and element count even when the shape buffer is too
small. Resize buffers outside the call and retry. Oversized read buffers keep their unused trailing
entries; oversized write buffers return `ShapeMismatch`. A separate metadata query and read can
observe different shapes, so use the combined overload when shape/data consistency matters.

Declare orchestra channels during setup, before concurrent host access. Runtime resizing through
`chnset` is supported, but do not redeclare an active channel with `chnarray`: in the shipped Csound
revision, that declaration publishes new storage without taking the channel lock. Arbitrary native
API calls that bypass the bridge's lifetime gate are likewise outside this synchronization contract.

The bridge re-reads pointers and shape on each operation; orchestra producers may resize their array
channel through the synchronized copy opcodes. Metadata, validation and copies share the same native channel lock. Host array calls
are serialized with a nonblocking gate: a concurrent host call returns `Busy` rather than waiting
behind another host copy. Reset and teardown share that gate so native memory cannot be freed beneath
a managed copy. Stop performance before calling the existing low-level reset API; this feature does
not make arbitrary concurrent orchestra compilation or reset safe.

There are no steady-state managed allocations in numeric copies or metadata reads with reused
buffers. UTF-8 names use bounded stack storage; no internal array pointers escape or are cached.
Native sample precision is checked before copying. These are synchronous copies, not a lock-free
transport: large arrays and host preemption can make the audio thread wait on the channel lock.
Keep transfers short and measure them on the target device. Initialization can allocate.

### String arrays

Declare `chnarray "labels", 3, "S", giShape` during orchestra setup, or initialize it from the host:

```csharp
var status = csound.InitializeArrayChannel("labels", CsoundArrayChannelType.String, 2, 2);
if (status != CsoundArrayChannelStatus.Success) return;
var sent = new[] { "C4", "caffè", "漢字", "🎹" };
var received = new string[4];
var sizes = new int[2];
status = csound.SetStringArrayChannel("labels", sent);
status = csound.GetStringArrayChannel("labels", received, sizes, out var info);
```

Use `SLabels[][] chnget "labels"` and `chnset SLabels, "echoLabels"` in the orchestra. Multidimensional
strings use the same flat ordering and shape rules as numeric arrays. `null` elements are written as
empty strings; reads return empty strings rather than null. Embedded NUL characters are rejected.
Numeric accessors reject string channels, and string accessors reject numeric channels with
`UnsupportedType`; metadata queries support both.

String transfers are intended for the main thread or another host thread, not audio callbacks
(`Busy`). Writes encode UTF-8 before acquiring the native channel lock. Reads copy a byte snapshot
under that lock, then decode after releasing it. Private byte/offset buffers grow outside the native
lock, with bounded retries if the producer changes in the meantime; a caller may need to retry `Busy`.
Reuse the destination array: unchanged text retains its existing string reference, while changed text
allocates a new managed string. Larger text may grow native storage during a write. These operations
are synchronous and are not a real-time messaging API. Private scratch buffers are cleared on reset
and teardown; no native pointers are retained.

WebGL players return `UnsupportedPlatform` explicitly. The browser fallback remains function tables
for numeric data, or a delimited string for string collections. An Editor running with WebGL as the
selected build target still uses the native implementation.


### Array sample and smoke checks

Import **Basic** and open `Array Channels/ArrayChannels.unity`. Move the four note
sliders, enable the k[] chord, and capture an i[] chord to compare continuous and init-rate reads.
The panel also shows a 2 × 3 matrix doubled by Csound, an orchestra producer switching between two
and seven elements, editable Unicode strings echoed as a 2 × 2 array, and a deliberately undersized read that must preserve its sentinel. Pause and
restart controls let you check that snapshots freeze and the rebuilt instance remains usable.

Import **Diagnostics** and open `API Smoke Test/ApiSmokeTest.unity` for repeatable pass/fail results.
The numeric-array section checks metadata, flat and shaped copies, non-resizing initialization,
wrong lengths/types, untouched buffers on failure, channel discovery and both numeric rates. Its
orchestra fixture acknowledges a completed control period before reads and verifies producer growth
and shrinkage. String checks cover Unicode, long text echoed through Csound, null/empty values,
capacity failures, type mismatches and producer growth/shrinkage. WebGL checks all array entry points
for `UnsupportedPlatform`. Results join the existing
console report; this does not replace listening to or profiling the interactive sample.
