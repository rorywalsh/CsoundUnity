## Lifecycle API ##

> **New in v4.0.0**

CsoundUnity exposes explicit control over the Csound performance lifecycle, including deferred initialisation, stop, and restart.

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

csound.LoadCsdFromString(csd); // parses, then (re)initialises
```

**Startup behaviour** (when the default `startNow: true` is used):

| State | Result |
|---|---|
| Already running | Reloaded via `Restart()` |
| Not running, `initializeOnAwake == false` | Started now via `Initialize()` |
| Not running, `initializeOnAwake == true` | Left for `Awake` to compile the stored string |

Pass `startNow: false` to only store and parse the CSD without (re)initialising — then call `Initialize()` yourself when ready:

```csharp
csound.LoadCsdFromString(csd, startNow: false);
// ... set up channels, routes, etc. ...
csound.Initialize();
```

> **Note:** if called before `Awake` has run (e.g. immediately after `AddComponent`), only the fields are populated; initialisation is deferred to `Awake` or a later `Initialize()` call, because `Init` depends on Awake-time state (DSP buffer size, AudioSource).

### IsInitialized ###

Read-only property. `true` once Csound has compiled successfully and the audio thread is running.

```csharp
if (csound.IsInitialized)
    csound.SetChannel("gain", 0.5);
```

### Events ###

| Event | Fires when |
|---|---|
| `OnCsoundInitialized` | `Initialize()` completes successfully |
| `OnCsoundStopped` | `Stop()` or `Restart()` is called |
| `OnCsoundPerformanceFinished` | The score ends naturally (all events complete, no `f0 z`) |

```csharp
void Start()
{
    csound = GetComponent<CsoundUnity>();
    csound.OnCsoundInitialized        += () => Debug.Log("Csound ready");
    csound.OnCsoundStopped            += () => Debug.Log("Csound stopped");
    csound.OnCsoundPerformanceFinished += () => Debug.Log("Score finished");
}
```

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

