# Loading external files in CsoundUnity

Csound can read many kinds of external resources at runtime — audio
files, sound fonts, plain text data, opcode plugins, and more. This
page covers how to make them reachable to Csound from a Unity project,
and the trade-offs between the available delivery mechanisms.

The mechanisms that physically deliver files to a location Csound can
open — using the `CopyFilesToPersistentDataPath` component, setting environment variables, etc. —
are documented in [Environment Variables](environment_variables.md);
this page focuses on the file-loading workflow and the trade-offs
between the available approaches.

---

## File types

### Audio files

Format support depends on the opcode you use:

- `diskin`, `diskin2`, `soundin`, and `GEN01` read audio through the
  [libsndfile](https://libsndfile.github.io/libsndfile/) library, which
  supports WAV, AIFF, AU/SND, RAW, FLAC, and Ogg Vorbis — plus MP3
  since libsndfile 1.1.0.
- `mp3in` uses a dedicated MP3 decoder and reads only MP3 files
  (mono or stereo).

For format-specific behaviour and limits, see the
[Csound manual](https://csound.com/docs/manual/index.html) entry for
each opcode below.

Csound-side loaders:

| Opcode | Auto-resamples to orchestra `sr` | Notes |
|---|---|---|
| `diskin2` (default `iwsize=4`) | Yes | Recommended for general playback |
| `diskin2` with `iwsize=1` | No | Faster but file plays at wrong pitch when its sr differs |
| `diskin` | No | Older / simpler; same pitch issue |
| `mp3in` | Yes | Mono / stereo MP3; carries its own sr in the bitstream |
| `soundin` | No | Streaming reader |
| `GEN01` (score `f` statement) | No | Loads the file into an ftable as raw samples |

CsoundUnity also offers a managed-side path: read the AudioClip in C#,
push the samples into a Csound function table, and read with `tab` /
`table` opcodes. Useful when the audio is already a Unity asset.

→ See [Sample rate considerations](#sample-rate-considerations) below
for what happens when the file sample rate differs from Csound's `sr`.

### Sound fonts (`.sf2`)

Loaded with the soundfont opcode family (`sfload`, `sfilist`, `sfinstr`,
`sfplay`, `sfplay3`, ...) — see the
[Csound manual](https://csound.com/docs/manual/SiggenSample.html) for the
full opcode reference.

Sound fonts are typically located via the `SFDIR` environment variable.
See the **Environment / SFDIR** sample for a complete working example.

### Plain text / data files

Generic data can be read with `GEN23` (text → ftable) and the file I/O
opcodes (`fout`, `fin`, `fink`, `fini`, `fiopen`, ...). Useful for
loading parameter sets, mapping tables, or analysis data.

### Csound opcode plugins (`.dll` / `.dylib` / `.so`)

Compiled libraries that add extra opcodes to Csound. Csound auto-loads
every plugin found in the directory pointed to by the `OPCODE6DIR64`
environment variable at startup.

The way to ship these libraries with a Unity project depends on the
platform:

- **Desktop (Windows / macOS)**: include the plugin libraries in your
  Unity project (e.g. via `CopyFilesToPersistentDataPath`) and point
  `OPCODE6DIR64` at the destination folder.
- **Mobile (Android, iOS)**: place the platform-specific native
  libraries (`.so` / `.dylib`) under `Plugins/<Platform>/<arch>/` in
  your Unity project. Unity bundles them into the build's native
  library directory automatically. Csound finds them at runtime via
  the dedicated `Plugins` base folder option in CsoundUnity's
  Environment Variables settings.

See the [Csound plugins manual](https://csound.com/docs/manual/CommandEnvironment.html)
for the loader details, [Environment Variables](environment_variables.md)
for the per-platform setup in CsoundUnity, and the **Environment /
Load Plugins** sample for a working configuration.

---

## Where Unity stores files

Unity offers two folders that matter for runtime file access. Each one
behaves differently, and the difference dictates which delivery
mechanism you need to use.

### `Resources/`

Files placed in any `Resources/` folder go through Unity's **asset
pipeline**. Unity tries to import each file as a recognised asset:

- `.wav` / `.aif` / `.mp3` / `.ogg` / ... → `AudioClip`
- `.txt` / `.json` / `.bytes` → `TextAsset`
- `.png` / `.jpg` → `Texture2D`
- ... etc.

Files Unity does **not** recognise are simply not imported — they will
not be present in the build at all. This is the case for
`.sf2` (sound fonts), `.dll` / `.dylib` / `.so` (opcode plugins),
`.mid`, and most non-mainstream binary formats.

The standard workaround is to **rename the file's extension to `.bytes`
or `.txt`** so Unity imports it as a `TextAsset`. The original
extension is restored at runtime when the file is written to disk
(this is exactly what `CopyFilesToPersistentDataPath` does — see
mechanism 3 below).

> Unity also re-encodes `AudioClip` assets into its own internal format
> at build time. The original file bytes are not preserved. Use
> `AudioSamplesUtils.GetSamples(AudioClip)` to recover the decoded
> samples in C#, but you cannot point Csound directly at the
> AudioClip's underlying file from disk.

### `StreamingAssets/`

Files in `Assets/StreamingAssets/` are copied **verbatim** into the
build — no asset import, no transformation. The bytes you put in are
the bytes you get out.

How they are reached depends on the platform:

- **Desktop / iOS**: files live on the file system at
  `Application.streamingAssetsPath`. Csound can open them directly with
  `diskin2`, `sfload`, etc.
- **Android**: `StreamingAssets` is packaged inside the compressed APK.
  The path returned by `Application.streamingAssetsPath` does **not**
  point at a regular file — it can only be read via `UnityWebRequest`.
  Csound has no way to perform a `UnityWebRequest`, so files need to be
  extracted to a real location on disk first.
  `Application.persistentDataPath` is that location;
  `CopyFilesToPersistentDataPath` performs the extraction
  transparently.

This means: **on Android, a file in `StreamingAssets` is not
accessible to Csound until it has been copied to
`Application.persistentDataPath`.**

---

## Delivery mechanisms

Putting it all together — the four ways to make a file available to
Csound at runtime:

### 1. AudioClip → in-memory function table

Audio only. The AudioClip is decoded by Unity's asset pipeline, the
samples are extracted with `AudioSamplesUtils.GetSamples`, and pushed
into a Csound function table with `CsoundUnity.CreateTable`. Csound
never touches the file system.

```csharp
var samples = AudioSamplesUtils.GetSamples(audioClip);    // float[] interleaved
csoundUnity.CreateTable(9000, samples);
// CSD reads from ftable 9000 with `tab` / `table`
```

The `TableLoader` component (under `Utilities/Components/`) wraps this
flow if you prefer not to script it.

- ✅ No file system / streaming concerns
- ✅ Fast random access from Csound (entire file resident in memory)
- ❌ Memory cost scales with clip length × channels × sample size
- ❌ AudioClip must be Decompress on load (or otherwise readable) so
  `GetSamples` can read it
- 🎵 Pitch shifts on `sr` mismatch (see below)

Sample: **Samplers / AudioClip Reader**.

### 2. AudioSource clip → Csound spin buffer (`processClipAudio`)

Audio only, **OnAudioFilterRead path only** (not available with
IAudioGenerator — see [IAudioGenerator](iaudiogenerator.md)). The
AudioSource plays the clip normally, and Unity's audio thread feeds
each block of samples into Csound's spin buffer where it appears as
`ain` / `ins` / `inch` in the orchestra.

- ✅ Real-time, no upfront load — works for arbitrarily long clips
- ✅ Uses Unity's existing AudioClip pipeline (compressed formats OK)
- ❌ OAFR-only; mutually exclusive with IAudioGenerator
- 🎵 Pitch shifts on `sr` mismatch (see below)

Sample: **Samplers / Process Clip Audio**.

### 3. `CopyFilesToPersistentDataPath` + Environment Variable

Universal mechanism that works on every platform — including Android —
and for **every** file type listed above. The component gathers files
from your Unity project (from `Resources` and / or `StreamingAssets`)
and writes them to `Application.persistentDataPath` before Csound
starts. Files are copied **only once on first launch**; on subsequent
launches the component skips any that already exist in the destination,
so startup stays fast. An environment variable (`SFDIR`, `SADIR`,
`SSDIR`, `OPCODE6DIR64`, ...) is then set to point Csound at that
directory.

> `Application.persistentDataPath` is reachable by the end user on
> most platforms (visible in the file system on desktop, browseable on
> Android). If the assets you ship need to remain private, consider
> shipping them encrypted in `Resources` and decrypting only when
> handing them to Csound, or use a different delivery strategy.

The component handles the Unity-side specifics for you. The four
input slots, summarised:

| Slot | Source | Result on disk |
|---|---|---|
| Audio files | `AudioClip` from Resources | Re-encoded as 16-bit WAV / AIFF from the imported AudioClip |
| Plugins | `TextAsset` (renamed `.bytes`) under `Resources/Plugins/Win` or `Resources/Plugins/MacOS` | `<name>.dll` (Windows) or `lib<name>.dylib` (macOS) |
| Additional files | `TextAsset` (renamed `.bytes` / `.txt`) from Resources | Original file restored with the extension you specify (`.sf2`, custom binary, …) |
| StreamingAssets files | Files in `Assets/StreamingAssets/...` | Same file copied to persistentDataPath; on Android extracted via `UnityWebRequest`, plain copy elsewhere |

> Audio files are **re-encoded** from the imported `AudioClip` — the
> original byte-for-byte file is not preserved. This means the sample
> rate of the file on disk is whatever Unity used for the AudioClip
> after import (controlled by the AudioClip Import Settings → Sample
> Rate Setting). If you need to preserve a specific sample rate, set
> the AudioClip's import settings explicitly, or place the file in
> StreamingAssets instead.

Use this approach whenever the file is not reachable any other way —
in particular it is **mandatory on Android** for any file Csound has
to read from disk.

See [Environment Variables → CopyFilesToPersistentDataPath](environment_variables.md)
for the full procedure and per-platform notes.

Samples: **Environment / SFDIR**, **Environment / Load Plugins**.

### 4. Direct read from `StreamingAssets` (desktop / iOS only)

If you only target desktop and / or iOS, you can skip the copy step
entirely: place the files in `Assets/StreamingAssets/...` and point
the relevant environment variable directly at
`Application.streamingAssetsPath`. Csound opens the files in place.

- ✅ No copy step, no rename, no asset-import transformation
- ✅ Works for any file format Csound can read (`.sf2`, `.wav`,
  custom binary, …)
- ❌ **Does not work on Android** — see the
  [previous section](#streamingassets) for why; use mechanism 3 there

This is the simplest mechanism when Android is not a target. For
cross-platform code, use mechanism 3 (which can copy *from*
StreamingAssets too, so you can keep your file layout consistent).

---

## Sample rate considerations

When the audio you load has a sample rate different from Csound's `sr`,
the **opcode reading the file** decides whether you get a pitch shift
or correctly-pitched playback.

The full sample-rate chain is:

```
audio file native sr  →  Csound sr  →  AudioSettings.outputSampleRate  →  audio device sr
```

Csound's `sr` follows `AudioSettings.outputSampleRate` by default
(see [Audio Rates: sr, kr, and ksmps](controlling_csound_from_the_unity_editor.md#audio-rates-sr-kr-and-ksmps)),
and Unity in turn negotiates with the OS. **The only mismatch you
actively need to manage is between the file and Csound's `sr`.**

### Which path resamples?

| Loading path | Resamples to Csound `sr`? | Pitch shift on mismatch? |
|---|---|---|
| Mechanism 1 — AudioClip → ftable + `tab` / `table` | No | Yes |
| Mechanism 2 — `processClipAudio` (spin buffer) | No | Yes |
| Mechanism 3 / 4 — `diskin2` (default `iwsize ≥ 2`) | Yes | No |
| Mechanism 3 / 4 — `diskin2` with `iwsize=1` | No | Yes |
| Mechanism 3 / 4 — `diskin` | No | Yes |
| Mechanism 3 / 4 — `mp3in` | Yes | No |
| Mechanism 3 / 4 — `soundin` | No | Yes |
| Mechanism 3 / 4 — GEN01 in score | No | Yes |

### What the pitch shift looks like

| File sr | Csound sr | Result with non-resampling opcode |
|---|---|---|
| 44100 | 44100 | original pitch ✓ |
| 44100 | 48000 | played ~8.8% faster → ~+1.5 semitones |
| 48000 | 44100 | played ~8.2% slower → ~−1.4 semitones |
| 22050 | 48000 | played ~2.2× faster → ~+1 octave + ~+3 semitones |

### Avoiding the transposition

- **Match the file sr to your project sr** in your DAW / audio editor
  before importing — easiest fix
- Prefer `diskin2` with default `iwsize` over `diskin` / `soundin`
  when reading from disk
- For raw-table playback (mechanisms 1, 2 or `GEN01`), apply the ratio
  yourself in the orchestra:
  ```csound
  iFileSr  = 44100              ; native sr of the audio file
  iRatio   = iFileSr / sr       ; sr is Csound's running rate
  aPhs phasor (iFileSr/iLen)*iRatio
  ```
- Pick a single project sample rate and set Unity → Project Settings →
  Audio → System Sample Rate to match it; import all audio files at
  that rate

### A note on `AudioSettings.outputSampleRate`

48000 Hz is the most common rate across modern devices and audio
interfaces, but the value is ultimately platform- and device-dependent.
If your project is multi-platform, your file may sound at the right
pitch in the editor but transposed in a build — or vice versa. Pinning
**System Sample Rate** in Project Settings → Audio is the simplest way
to keep the chain consistent.

> Some Android devices report a default output sample rate of 24000 Hz
> even though they are capable of running at 48000 Hz. If you hear
> obvious pitch shifts on Android, set **System Sample Rate** to 48000
> in Project Settings → Audio explicitly before building.
