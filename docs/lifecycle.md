## Lifecycle API ##

> **New in v4.0.0**

CsoundUnity exposes explicit control over the Csound performance lifecycle: deferred initialisation, stop, restart, and swapping the orchestra of a live instance.

### initializeOnAwake ###

By default, CsoundUnity compiles and starts Csound automatically in `Awake`. If you need to delay initialisation (e.g. to load files first, or to start on demand), disable **Initialize On Awake** in the inspector and call `Initialize()` manually.

```csharp
csound.initializeOnAwake = false; // or untick in inspector

IEnumerator Start()
{
    // do some setup first…
    yield return LoadAssets();

    csound.Initialize();
    while (!csound.IsInitialized)
        yield return null;

    // Csound is ready
}
```

### Initialize() ###

Compiles the `.csd` file and starts the Csound performance. Has no effect if Csound is already running.

```csharp
csound.Initialize();
```

### Stop() ###

Stops the Csound performance and releases the audio thread. Fires `OnCsoundStopped`.

```csharp
csound.Stop();
```

### Restart() ###

Stops and re-initialises Csound. Useful for hot-reloading a `.csd` file at runtime without leaving Play mode. Fires `OnCsoundStopped` followed by `OnCsoundInitialized` when ready.

```csharp
csound.Restart();
```

### Swapping the orchestra at runtime ###

`Restart()` throws the Csound instance away and builds a new one from the same `.csd`. The three
calls below keep the instance and swap the orchestra inside it, which is what you want when the new
orchestra is not in the project: generated at runtime, downloaded, or sent by another system. The
reset can come first and the orchestra arrive much later, because audio stays silent in between.

The order is fixed, and every call reports whether it worked:

```csharp
if (!csound.CsoundReset()) return;                // the orchestra is gone, audio goes silent

// …wait as long as you need. The orchestra can arrive whenever it arrives…

if (csound.CompileOrc(orchestra) != 0) return;    // compiled, audio still silent
if (csound.CsoundStart() != 0) return;            // performance on, audio resumes

csound.SendScoreEvent("i1 0 -1");
csound.SetChannel("gain", 0.5);
```

If that is more than you need, `Restart()` is simpler and is the path the samples take.

#### CsoundReset() ####

Returns the engine to the state it was in before compilation, keeping the instance alive. The
orchestra, the score and the control channels are gone. The host sample rate, ksmps and audio IO are
re-armed, so the instance keeps the audio format it was created with.

Audio callbacks are held silent from here until `CsoundStart()`, and `HasCompiled` is `false` for as
long as nothing is compiled. `IsInitialized` stays `true`, and no lifecycle event fires:
`OnCsoundStopped` belongs to `Stop()` and `Restart()`.

Returns `false` and leaves the engine as it was when the component is not initialised, when the call
comes from inside this instance's own audio callback, or when that callback does not return within
200 ms. Even a refused reset can cost up to 200 ms of silence, because the callbacks are held while
it waits.

#### CompileOrc() ####

Compiles an orchestra from a string and returns `0` on success. After a reset, compiling does not
resume audio: it only makes the orchestra available, so you can compile the moment it arrives and
start when it suits you. On an instance that was not reset, it adds instruments and UDOs to the
running performance without interrupting it.

When the orchestra does not compile, the return value is non-zero and Csound's own parser messages
reach the Unity console, with the line and the columns:

```
unable to find opcode with name: not_an_opcode, line 2, columns 2-14
```

A successful compilation is quiet by comparison. Csound prints its header, the rates and the channel
counts when the performance starts, not when the orchestra compiles.

#### CsoundStart() ####

Starts the compiled orchestra and lets the audio callbacks back in. Returns `0` on success, and `0`
again when the instance is already running, so calling it twice is harmless and never restarts
anything. It returns non-zero when nothing has been compiled yet.

Before audio resumes it re-reads `0dbfs`, `ksmps` and `nchnls` from the new orchestra and rebuilds
the `main_out_0` … `main_out_N` channels, so the new orchestra can declare a different channel count
than the one it replaced.

#### What does not come back on its own ####

| What | How to get it back |
|---|---|
| The score | `SendScoreEvent()`. An empty score keeps the performance alive and silent, it does not end it |
| Control channels and their values | `SetChannel()`, which creates the channel if the new orchestra reads one with the same name. The Control Channels list in the inspector still describes the `.csd` the component was built from, not the orchestra you just compiled |
| Named audio channels | `AddAudioChannel()` for each channel the new orchestra writes, if you route it or read it from a `CsoundUnityChild` |

Score events sent between `CompileOrc()` and `CsoundStart()` are kept and play once the performance
starts, so the events can travel with the orchestra.

#### On WebGL ####

`CompileOrc()` works in a WebGL build. `CsoundReset()` returns `false` and `CsoundStart()` returns
non-zero there: to swap a CSD, use [LoadCsdFromString()](lifecycle.md#loadcsdfromstring) or
`Restart()`.

### LoadCsdFromString() ###

Loads a CSD from a raw string at runtime, without requiring a `.csd` asset in the project. This is the runtime counterpart of `SetCsd()` (which is editor-only and works via an asset GUID). Use it for CSDs that are generated at runtime, downloaded, or otherwise held in memory — the Csound bridge compiles directly from the stored string, so no file is needed.

Channel controllers, named audio channels, `nchnls` and `ksmps` are parsed from the string exactly as `SetCsd()` parses them from a file.

```csharp
string csd = @"<CsoundSynthesizer>
<CsInstruments>
sr = 48000
ksmps = 32
nchnls = 2
0dbfs = 1
instr 1
    aout oscili 0.2, 440
    outs aout, aout
endin
</CsInstruments>
<CsScore>
i 1 0 3600
</CsScore>
</CsoundSynthesizer>";

if (csound.LoadCsdFromString(csd)) // parses, (re)initialises, and says whether it runs
    Debug.Log("playing");
```

**Startup behaviour** (when the default `startNow: true` is used):

| State | Result | Returns |
|---|---|---|
| Already running | Reloaded via `Restart()` | whether it compiled |
| Not running | Started now via `Initialize()` | whether it compiled |
| `Awake` has not run yet | Only stored and parsed; `Awake` compiles it | `false` |
| `startNow: false` | Only stored and parsed | `false` |

The return value answers **"is Csound running this CSD now"**, not "was the file readable" — a CSD that arrives intact but fails to compile returns `false`, with the Csound error in the console. `false` is therefore not always a failure: asking for `startNow: false`, or calling before `Awake`, also returns it because nothing is playing yet.

`initializeOnAwake` does not change this. Once `Awake` has run it will not run again, so there is nobody else left to compile the string — including after a failed compile or a `Stop()`.

Pass `startNow: false` to only store and parse the CSD without (re)initialising — then call `Initialize()` yourself when ready:

```csharp
csound.LoadCsdFromString(csd, startNow: false);
// ... set up channels, routes, etc. ...
csound.Initialize();
```

> **Note:** if called before `Awake` has run (e.g. immediately after `AddComponent`), only the fields are populated; initialisation is deferred to `Awake` or a later `Initialize()` call, because `Init` depends on Awake-time state (DSP buffer size, AudioSource).

### LoadCsdFromPath() ###

Reads a CSD from a file and hands it to `LoadCsdFromString()`. There are two overloads, and which one you need depends on where the file is.

**Synchronous** — for real filesystem paths: `Application.persistentDataPath`, an absolute path, a file the user picked.

```csharp
var path = Path.Combine(Application.persistentDataPath, "patch.csd");
if (csound.LoadCsdFromPath(path))
    Debug.Log("loaded");
```

It returns whatever `LoadCsdFromString()` returns — **is Csound running this CSD now** — so a missing file, an unreadable one and a CSD that fails to compile all give `false`, each with its own message in the console.

**With a callback** — for **StreamingAssets** and for http/https URLs. The read goes through `UnityWebRequest`, so it works on every platform:

```csharp
var path = Path.Combine(Application.streamingAssetsPath, "patch.csd");
csound.LoadCsdFromPath(path, ok =>
{
    if (ok) Debug.Log("loaded");
});
```

> **Why two of them.** On **Android** and **WebGL**, `StreamingAssets` lives inside the compressed application package and cannot be opened with `File.ReadAllText` — the call that works in the editor fails on device. Rather than let that fail quietly, the synchronous overload detects such a path and refuses with an explicit error telling you to use the callback overload. On iOS, visionOS and desktop, StreamingAssets is an ordinary directory and either overload works.

The callback overload runs a coroutine, so the component must be active and enabled.

> **You do not have to stop Csound first.** Both overloads hand the content to `LoadCsdFromString()`, which reloads a running instance with `Restart()` — the startup table above applies unchanged. With the callback overload the read takes a few frames, during which the current CSD keeps playing; the swap happens when the file arrives.

A third option, if you would rather keep everything synchronous, is to copy the files out of StreamingAssets once at startup — see [CopyFilesToPersistentDataPath](loading_external_files.md) — and then read them from `persistentDataPath`.

### IsInitialized ###

Read-only property. `true` once Csound has compiled successfully and the audio thread is running.

```csharp
if (csound.IsInitialized)
    csound.SetChannel("gain", 0.5);
```

### Events ###

| Event | Fires when |
|---|---|
| `OnCsoundInitialized` | initialization completes successfully |
| `OnCsoundStopped` | `Stop()` or `Restart()` is called |
| `OnCsoundPerformanceFinished` | The score ends naturally (all events complete, no `f0 z`) |

#### When each call completes ####

| Call | Compiles | `OnCsoundInitialized` fires |
|---|---|---|
| `initializeOnAwake` on, every platform but WebGL | in `Awake`, synchronously | before `Awake` returns |
| `initializeOnAwake` on, WebGL | in the background, once the WASM bridge is ready | later, from the bridge callback |
| `Initialize()` called by hand | same as above, when the call is made | same as above |
| `Restart()` | synchronously, inside the call | before the call returns |
| `LoadCsdFromString(csd)` | synchronously; the returned `bool` already says whether it compiled | before the call returns |

**Subscribing in `Start` is too late on every platform but WebGL.** `Awake` runs before any `Start`,
so by then the event has already fired and a handler attached in `Start` never runs. Handle the case
where it is already up:

```csharp
using Csound.Unity;
using UnityEngine;

public class ReadyWhenCsoundIs : MonoBehaviour
{
    private CsoundUnity _csound;

    void Start()
    {
        _csound = GetComponent<CsoundUnity>();

        if (_csound.IsInitialized) OnReady();                 // already up: Awake did it
        else _csound.OnCsoundInitialized += OnReady;          // WebGL, or initializeOnAwake off
    }

    void OnDestroy()
    {
        if (_csound != null) _csound.OnCsoundInitialized -= OnReady;
    }

    void OnReady() => _csound.SetChannel("gain", 0.5);
}
```

Unsubscribe in `OnDestroy`: the event lives on the CsoundUnity instance, so a handler on a destroyed
object keeps it alive and runs again if Csound is restarted.

If all you need is to read or write a channel every frame, the `IsInitialized` check in `Update` is
simpler and works the same on every platform — see
[The essentials](getting_started.md#waiting-for-initialisation).

### Typical patterns ###

**Wait in a coroutine:**

```csharp
IEnumerator Start()
{
    csound = GetComponent<CsoundUnity>();
    while (!csound.IsInitialized)
        yield return null;
    csound.SendScoreEvent("i1 0 -1");
}
```

**React to events:**

```csharp
void Start()
{
    csound = GetComponent<CsoundUnity>();
    csound.OnCsoundInitialized += StartInstrument;
}

void StartInstrument()
{
    csound.SendScoreEvent("i1 0 -1");
}
```

