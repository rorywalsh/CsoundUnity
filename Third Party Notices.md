# Third Party Notices

CsoundUnity's own code is MIT licensed (see `LICENSE`). The native libraries redistributed with the
package are not, and neither is some of the material in the samples. This file accounts for both —
libraries first, samples at the end.

Some of these are not separate files: the Csound binaries have other libraries **linked inside
them**, so they travel with the package even though you never see them in the folder listing. They
are listed here for that reason.

---

## Csound

- **Licence:** LGPL 2.1 — full text in `LICENSE-LGPL-2.1.txt`
- **Copyright:** The Csound developers (Barry Vercoe, John ffitch, Victor Lazzarini, Steven Yi,
  Michael Gogins, and many others)
- **Source:** https://github.com/csound/csound
- **Version:** 7.x

| Platform | File | Linkage |
|---|---|---|
| Windows | `Runtime/Win64/csound64.dll` | dynamic |
| macOS | `Runtime/macOS/CsoundLib64.bundle` | dynamic |
| Android | `Runtime/Android/{arch}/libcsoundandroid.so` | dynamic |
| iOS | `Runtime/iOS/CsoundiOS.xcframework` | **static** |
| visionOS | `Runtime/visionOS/libcsound.a` | **static** |
| WebGL | Csound WASM, fetched at runtime from `@csound/browser` | — |

## Libraries linked inside the binaries

Where each one actually ends up depends on the platform, and on which binary it was linked into.
Checked against the binaries in this repository by **defined symbols** rather than by strings, since
a string only proves a library was mentioned, not that its code is there:

| | Windows `csound64.dll` | macOS `CsoundLib64` | Android `libsndfile.so` | iOS `libsndfile` | visionOS `libsndfile.a` |
|---|---|---|---|---|---|
| libFLAC | yes | yes | yes | — | yes |
| libogg | yes | yes | yes | a few symbols | yes |
| libvorbis | yes | yes | yes | — | yes |
| libopus | yes | yes | — | — | — |
| LAME | yes | yes | — | — | — |
| mpg123 | yes | yes | — | — | — |
| libsamplerate | yes | yes | — | — | — |
| libsndfile | yes | yes | (is this file) | (is this file) | (is this file) |

On Apple platforms and Android, Csound itself bundles **none** of these: `libcsoundandroid.so`,
`libCsoundLib` and `libcsound.a` only *reference* libsndfile, which ships as its own file. On Windows
and macOS everything is welded into the one Csound binary — the Windows DLL imports nothing but
`KERNEL32`, `SHLWAPI` and `WS2_32`, and the macOS bundle nothing but system libraries.

> **PortMidi is not redistributed**, although Csound can use it. In Csound's build it is a separate
> plugin module — `make_plugin(pmidi pmidi.c)` in `InOut/CMakeLists.txt`, built only when PortMIDI is
> found — so it is never inside the Csound library, and we ship no plugin modules at all: no `pmidi`,
> no `ipmidi`, no `rtpa`, no opcode plugin folder. The name appears as a string in the Windows and
> macOS binaries because the core knows the names of the I/O modules it can load, not because the
> library is there. CsoundUnity's MIDI is its own code (`CsoundUnityMidiInput`, CoreMIDI on Apple
> platforms, `android.media.midi` on Android, WinMM on Windows).

*How to re-check*: `nm -g <file>` for the Mach-O and static archives, `nm -D <file>` for the Android
`.so`, then count symbols by prefix (`FLAC__`, `ogg`, `vorbis`, `opus_`, `lame_`, `mpg123_`,
`sf_`, `src_`), keeping defined and undefined apart.

| Library | Licence | Copyright / project |
|---|---|---|
| libsndfile | LGPL 2.1 | Erik de Castro Lopo — https://github.com/libsndfile/libsndfile |
| libFLAC | BSD 3-Clause | Xiph.Org Foundation — https://xiph.org/flac/ |
| libogg | BSD 3-Clause | Xiph.Org Foundation — https://xiph.org/ogg/ |
| libvorbis | BSD 3-Clause | Xiph.Org Foundation — https://xiph.org/vorbis/ |
| libopus | BSD 3-Clause | Xiph.Org Foundation — https://opus-codec.org/ |
| libsamplerate 0.2.2 | BSD 2-Clause | Erik de Castro Lopo — https://github.com/libsndfile/libsamplerate |
| LAME | LGPL 2.1 | The LAME project — https://lame.sourceforge.io/ |
| mpg123 | LGPL 2.1 | The mpg123 project — https://www.mpg123.de/ |

## libsndfile, shipped separately as well

- **Licence:** LGPL 2.1 — **Version:** 1.0.25 on Android
- `Runtime/Android/{arch}/libsndfile.so` (dynamic), `Runtime/iOS/libSndfileiOS.xcframework`
  (**static**), `Runtime/visionOS/libsndfile.a` (**static**)

On Android and visionOS this file **also carries libFLAC, libogg and libvorbis inside it** — see the
table above. They are BSD, so they ask only to be credited, which the table does; but it does mean
the LGPL obligation on these platforms rests on this file, not on the Csound binary.

## CsoundUnity native plugins

`CsoundNativeInput` (`Win64/CsoundNativeInput.dll`, `macOS/CsoundNativeInput.bundle`,
`Android/{arch}/libcsnativeinput.so`, `iOS/libCsoundNativeInput.a`) and
`Plugins/Android/CsoundUnityMidi.aar` are part of CsoundUnity, MIT licensed like the rest.

- **Source:** https://github.com/giovannibedetti/CsoundUnityNativeTools

---

## What the LGPL asks of you when you ship a game

Worth reading, because it lands on you rather than on us. It concerns Csound, libsndfile, LAME and
mpg123; the BSD and MIT libraries above only ask to be credited, which this file does.

The LGPL lets you use these libraries from code under any licence, including closed-source and
commercial. Nothing here forces your game to be open source. What it asks is that whoever receives
your game can **replace the LGPL library with their own build of it**, and that you say the
libraries are there and where their source lives.

How hard that is depends on the linkage:

**Dynamic — Windows, macOS, Android.** Already satisfied. The library is its own file in your build,
and anyone who wants to swap it can. Keep this notice, and do not merge the library into your
executable.

**Static — iOS and visionOS.** Not satisfied by shipping the app alone. The library is welded into
your binary, so the only way to let someone relink is to hand them what they would need: your object
files, or your source. LGPL 2.1 §6 is explicit about it. Projects in this position generally either
provide object files on request, or open their source, or move to a dynamic build.

> **TODO** — the clean fix is to ship Csound and libsndfile as embedded **dynamic** frameworks on
> iOS and visionOS, allowed since iOS 8. That settles it once for every user, instead of leaving
> each of them to deal with it. Until then it belongs in the platform documentation too, not only
> here.

Nothing here is legal advice. If you are shipping commercially on iOS, the static linking question
is worth ten minutes of someone qualified.

---

# Third-party material in the samples

The scenes under `Samples~` carry other people's work: instrument definitions, and recorded sound.
CsoundUnity's own code is MIT, these are not. Importing a sample copies these files into your
project, so what they ask applies to you.

## Read this one first: six files are non-commercial

| | |
|---|---|
| **Author** | Iain McCurdy |
| **Licence** | CC BY-NC-SA 4.0 — https://creativecommons.org/licenses/by-nc-sa/4.0/ |

- `FMSynthesis/Theremin/Theremin.csd`
- `WebGL/TestWebGL/BinauralTest.csd`
- `Miscellaneous/Csound Haiku/Resources/All_Haikus.csd`
- `Timelines/Step/StepTimeline.csd`
- `Presets/Voice Changer/VoiceChanger.csd`
- `Samplers/Dr B. Samplers/Resources/CAB-RCB-pvsBlur.csd`

**NonCommercial** means these six `.csd` files cannot go into something you sell, and **ShareAlike**
means a modified version has to carry the same licence. That is a stricter rule than anything else in
this package, and it is easy to miss, because the samples are meant to be copied and edited. They are
there to be learned from. If you want one of these instruments in a commercial game, ask Iain McCurdy
or write your own. The licence terms are stated in full at the top of each file.

## Author and licence known

| File | Author | Licence | What it asks |
|---|---|---|---|
| `UI/XYPad/XYPad.csd`, `Miscellaneous/XY Pad Test/XY Pad Test.csd` | Rory Walsh, 2021, ported by Giovanni Bedetti | CC0 1.0 | nothing |
| `Sequencers/Simple Sequencer/Resources/Samples/*.wav` — nine xylophone recordings — and `Samplers/AudioClip Reader/Resources/Samples/`, which reuses one of them plus a reversed copy | DANMITCH3LL, via Freesound (sounds 232001–232009) | CC BY 4.0 | credit the author; the reversed copy is a derivative, which the licence allows |
| `Miscellaneous/Trapped in Convert/Resources/trapped.csd` — *Trapped in Convert*, 1979 | Richard Boulanger | LGPL 2.1+ | ships inside Csound's own `examples/`, so it stands on the same footing as Csound |
| `Environment/Load Plugins/Resources/scanu2.csd` | John ffitch | GFDL 1.2+ | from the Csound Manual, **not** the LGPL that covers Csound; licence text in that sample's folder |
| `Samplers/Process Audio Clip/Resources/Guitar3.wav` | Cabbage — https://github.com/rorywalsh/cabbage | GPL 3.0 | from Cabbage's `Examples/Widgets/`; licence text in that sample's folder |
| `fox.wav` — in `Presets/Voice Changer`, `WebGL/TestWebGL`, `GranularSynthesis/Partikkel/Resources` and `Samplers/Process Audio Clip/Resources` | the Csound project | LGPL 2.1+ | from Csound's own `tests/commandline/`, and in CsoundQt's `SourceMaterials`; licence text in the package root |

## Attributed, but no licence stated upstream

These name their authors and we have kept that, but no licence was declared with the originals. They
are being clarified with the people involved. Until then, treat them as all rights reserved by their
authors and ask before using them in something you ship.

| File | Credited to |
|---|---|
| `Samplers/Dr B. Samplers/Resources/CAB-RCB-flooper.csd`, `CAB-RCB-mincer.csd`, and the 20 recordings in `Resources/sounds/` | “Sound design and presets by Dr. Richard Boulanger and his students at Berklee”, ported by Giovanni Bedetti, 2023 |
| `Engines/drive_engines.csd` and the `m_scene_*.udo` files | Jeanette C., from a CSD by Oeyvind Brandtsegg; one UDO follows a model described by Andy Farnell in *Designing Sound* |
| `GranularSynthesis/Partikkel/partikkel-2.csd` | “Example by Joachim Heintz and Oeyvind Brandtsegg 2008” |

