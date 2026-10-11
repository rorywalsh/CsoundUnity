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

