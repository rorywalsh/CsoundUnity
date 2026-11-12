## How to import CsoundUnity ##

CsoundUnity is distributed as a **Unity Package**. Use the **Unity Package Manager** to add it to your project. Open the Package Manager via **Window → Package Manager**.

### Install via Git URL ###

In Unity, open **Window → Package Manager**, press **+**, choose **Add package from git URL…**, paste one of the URLs below, and press **Add**.

#### Stable 3.5.2 (default branch)

The URL without a tag or branch uses the repository's default branch, `master`, which currently contains **3.5.2**. See the [3.x documentation](https://rorywalsh.github.io/CsoundUnity/v3/).

```
https://github.com/rorywalsh/CsoundUnity.git
```

#### 4.0.0-alpha.2 (tagged prerelease)

Append a published tag to install a specific build, including newer releases that haven't been merged into `master` yet. The 4.0 prerelease covered by this guide is **v4.0.0-alpha.2**.

```
https://github.com/rorywalsh/CsoundUnity.git#v4.0.0-alpha.2
```

See [GitHub Releases](https://github.com/rorywalsh/CsoundUnity/releases) for release notes and other tags.

#### 4.0 development branch

Use the branch name after `#` to try the latest commits on the 4.0 development branch. It can include changes that are still in testing.

```
https://github.com/rorywalsh/CsoundUnity.git#release_4_0_0
```

Unity records the resolved commit in `Packages/packages-lock.json`. To fetch newer commits from the branch, add the same Git URL again in Package Manager. You can also use any of the URLs above as the value of `com.csound.csoundunity` in your project's `Packages/manifest.json`.

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

Then choose a version published on OpenUPM and use its version number in `manifest.json` or in your own package's
`package.json`. Replace `<version>` with that number:

```json
"dependencies": {
  "com.csound.csoundunity": "<version>"
}
```

### Install from disk ###

Download the source code (zip or tar.gz) of the [release you chose](https://github.com/rorywalsh/CsoundUnity/releases), extract it, press **+** in the Package Manager, select **Add package from disk…**, and choose the `package.json` inside the extracted folder.

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
