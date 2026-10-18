## How to import CsoundUnity ##

CsoundUnity is distributed as a **Unity Package**. Use the **Unity Package Manager** to add it to your project. Open the Package Manager via **Window → Package Manager**.

### Install via git URL ###

Press **+**, select **Add package from git URL…**, paste the following URL and press **Add**:

```
https://github.com/rorywalsh/CsoundUnity.git
```

### Install a specific version ###

The URL above tracks the default branch, so it gives you whatever is on it at the moment you add the
package — and `packages-lock.json` then pins that exact commit for the project. To pin a release
instead, append the tag:

```
https://github.com/rorywalsh/CsoundUnity.git#v4.0.0
```

Tags follow the version, `v4.0.0`, `v3.5.2`, and the same string works in the Package Manager window
or written straight into `Packages/manifest.json`:

```json
"dependencies": {
  "com.csound.csoundunity": "https://github.com/rorywalsh/CsoundUnity.git#v4.0.0"
}
```

### Depending on CsoundUnity from another package ###

A git URL works in a **project's** manifest, but Unity will not resolve one listed in a **package's**
`dependencies`. For that the package has to come from a registry, and CsoundUnity is published on
[OpenUPM](https://openupm.com/packages/com.csound.csoundunity/).

Add the scoped registry to the consumer project's `Packages/manifest.json`:

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": [ "com.csound.csoundunity" ]
  }
]
```

and then a plain version is enough, both in `manifest.json` and in your own package's
`package.json`:

```json
"dependencies": {
  "com.csound.csoundunity": "4.0.0"
}
```

### Install from disk ###

Download the source code (zip or tar.gz) of the [latest release](https://github.com/rorywalsh/CsoundUnity/releases/latest), extract it, press **+** in the Package Manager, select **Add package from disk…**, and choose the `package.json` inside the extracted folder.

---

## Upgrading from v3.x to v4.0.0 ##

v4.0.0 requires **Unity 2022.3 LTS or later**, which is also the floor the Asset Store accepts.
Unity 6 is required for the IAudioGenerator and RootOutput audio paths: on 2022.3 only
`OnAudioFilterRead` is available, and that is the default on every version, so upgrading does not
move an existing project onto another path.

The [migration guide](migration_3_to_4.md) lists every breaking change. The three that account for
most of the work in an upgrade:

- **the `Csound.Unity` namespace** — every script that references CsoundUnity needs
  `using Csound.Unity;`. This is the most common source of compile errors after the update
- **`mute`** silences the instance while Csound keeps performing, instead of freezing it
- **combobox channels** carry the value Cabbage uses, counting from 1. A csd that added 1 itself to
  work around the old behaviour now lands one option further along, and nothing reports it

The native libraries are now **Csound 7**. The `CsoundUnity` API is unchanged by this, but twelve
public methods of `CsoundUnityBridge` are gone with the C functions behind them — see
[CsoundUnityBridge](migration_3_to_4.md#csoundunitybridge-api-changes) in the migration guide.
