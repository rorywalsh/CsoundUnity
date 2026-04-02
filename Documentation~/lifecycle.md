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

