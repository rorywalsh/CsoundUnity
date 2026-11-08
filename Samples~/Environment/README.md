# Environment

The scenes that load external files use `CopyFilesToPersistentDataPath`. Files are copied before
Csound starts, with environment paths configured to match their destination.

- **SFDIR**: three startup examples with the same soundfonts — activation after copying, explicit
  initialization of an already active object, and prefab instantiation after copying.
- **Load Plugins**: copies the opcode plugin and matrix data, then explicitly initializes an active
  Csound object. Input is ignored until Csound is initialized.
- **Read Environment Paths**: reports configured paths and the values Csound receives. This diagnostic
  has no files to copy and deliberately initializes Csound without an orchestra.

`EnvironmentSampleInitializer` is shared by the manual and prefab examples. It calls `Copy()`, waits
until `IsCopying` is false, verifies `copyCompleted`, and only then obtains the Csound instance. It
subscribes to `OnCsoundInitialized` before calling `Initialize()`. `Instance` exposes the target;
`IsReady` becomes true when initialization reports a successfully compiled orchestra.

For these manual examples, disable **Initialize On Awake** on the scene object or prefab. The object
can remain active: `Awake` prepares the component, while explicit `Initialize()` starts Csound. The
copy component's activation list is empty and **Auto Start** is off, since the initializer controls
both stages. None of this requires changing `CopyFilesToPersistentDataPath` to instantiate prefabs.
