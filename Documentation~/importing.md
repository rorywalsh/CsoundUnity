## How to import CsoundUnity ##

CsoundUnity is distributed as a **Unity Package**. Use the **Unity Package Manager** to add it to your project. Open the Package Manager via **Window → Package Manager**.

### Install via git URL ###

Press **+**, select **Add package from git URL…**, paste the following URL and press **Add**:

```
https://github.com/rorywalsh/CsoundUnity.git
```

### Install from disk ###

Download the source code (zip or tar.gz) of the [latest release](https://github.com/rorywalsh/CsoundUnity/releases/latest), extract it, press **+** in the Package Manager, select **Add package from disk…**, and choose the `package.json` inside the extracted folder.

---

## Upgrading from v3.x to v4.0.0 ##

v4.0.0 is a **major release** with breaking changes. Please read this section before upgrading.

### Csound 7 ###

The native libraries have been updated to **Csound 7**. Several C API functions have been renamed or removed. If you have written code that calls `CsoundUnityBridge` or `CsoundCsharp` methods directly, review the [Csound 7 API changes](https://github.com/csound/csound/blob/master/ChangeLog) and update your calls accordingly.

The most commonly affected methods are: `csoundCreate`, `csoundCompileOrc`, `csoundCompileCSD`, `csoundEventString`, and `csoundGetChannels`.

### IAudioGenerator (Unity 6+) ###

On Unity 6, the default audio path has changed from `OnAudioFilterRead` to **IAudioGenerator**. Existing scenes will continue to work — the serialised `AudioPath` field defaults to `IAudioGenerator` only on new components. If you need the old behaviour, set **Audio Path → OnAudioFilterRead** in the inspector.

### Namespace ###

v4.0.0 introduces the `Csound.Unity` namespace. All CsoundUnity types are now inside this namespace, so you must add the following using directive to every script that references CsoundUnity:

```csharp
using Csound.Unity;
```

This is the most common source of compile errors when upgrading from v3.x.

### Minimum Unity version ###

v4.0.0 requires **Unity 2020.2 or later**. Unity 6 is required for IAudioGenerator features.
