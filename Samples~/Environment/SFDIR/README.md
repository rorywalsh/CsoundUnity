# SFDIR

All three scenes copy the two soundfonts from `Resources` with `CopyFilesToPersistentDataPath`.
Their SFDIR settings point to the destination, `Application.persistentDataPath`. Unity imports the
sources as `.bytes`; `Additional Files` specifies their destination extension as `sf2`.

| Scene | Startup |
|---|---|
| **SFDIR.unity** | The Csound object starts inactive with Initialize On Awake enabled. After successful copying, the copier activates it and Csound initializes automatically. |
| **SFDIR_Alternative.unity** | The Csound object is already active, with Initialize On Awake disabled. `EnvironmentSampleInitializer` waits for a successful copy, subscribes to `OnCsoundInitialized`, then calls `Initialize()`. |
| **SFDIR_Prefab.unity** | No Csound object exists in the scene initially. The same initializer copies the files first, instantiates `Csound.prefab`, subscribes to the event and explicitly initializes the new instance. |

The shared initializer exposes `Instance` and `IsReady`, and logs when files are available and when
Csound reports a compiled orchestra. Its copier has Auto Start disabled and an empty activation
list so that startup is controlled entirely by the example script.

If copying fails, no Csound instance is initialized or instantiated by the initializer. Inspect
`IsCopying`, `copyCompleted` and `FailedFileCount` on the copier to distinguish completion and errors.
Existing destination files are reused. To test a fresh copy, remove only `sf_GMbank.sf2` and
`07AcousticGuitar.sf2` from the persistent-data directory before entering Play mode.
