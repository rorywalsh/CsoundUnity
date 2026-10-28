# CsoundUnity #

**Documentation for version 4.0.0.**

3.x docs available [here](https://rorywalsh.github.io/CsoundUnity/v3/).

**CsoundUnity is a fully integrated audio middleware.** It extends Unity's audio API by creating
Csound-based AudioSource objects that can interact and exist alongside regular AudioSource objects.
With CsoundUnity, games developers have at their fingertips one of the most powerful synthesis
systems in existence.

Write an instrument, drop it on a GameObject, press play.

* **Sound that is computed, not played back.** Every shot, footstep and engine note is made on the
  spot, out of what the game is doing: no files to loop, no clips to crossfade.
* **Deep Timeline integration.** Clips that fire score events, clips that automate a channel, and
  sequencers on the same track: patterns, step sequencer, arpeggio, euclidean, chord, stochastic
  and swarm.
* **Multichannel audio in, fast.** The native audio API of each platform, every channel the device
  has, at a latency measured in a few milliseconds.
* **MIDI in** from keyboards and controllers, straight to the orchestra.
* **Spatial like everything else in your scene.** Csound comes out of an AudioSource, so panning,
  distance and mixer groups apply, and the orchestra adds what Unity cannot: a true
  [Doppler](doppler.md) shift and the delay of sound travelling.
* **Audio routed between instances.** A named channel of one csd becomes the input of another, so
  an instrument, an effect and a bus are separate objects in the scene, wired in the editor with
  the Route Graph. See [Audio Input Routing](audio_input_routing.md).
* **Presets** that capture every channel and recall it mid-scene, saved as assets or JSON, and
  Cabbage snaps imported.
* **A UI straight from your csd.** The widgets the file declares become a Unity Canvas at the press
  of a button: sliders, knobs, XY pads, keyboards, meters, all wired to their channels.
* **See what you hear.** Waveform, spectrum, spectrogram, Lissajous and oscilloscope, live in the
  inspector while the scene runs.
* **Record it.** What Unity finally plays, written to WAV or AIFF, from the editor or from a build.
* **Everywhere Unity goes**, with Csound 7 on macOS, Windows, iOS, Android and visionOS, and the
  WebAssembly build on WebGL. See [Supported Platforms](platforms.md).
* **Samples for every part of it**, each a scene you can open and play. See [Samples](samples.md).

---

### Get started ###

  * [How to import CsoundUnity](importing.md)
  * [Quick Start](quick_start.md) — from nothing to a sound you can change, in one page
  * [Coming from Unity](coming_from_unity.md) — for Unity developers meeting Csound for the first time
  * [Coming from Csound](coming_from_csound.md) — for Csound users meeting a game engine for the first time
  * [The essentials](getting_started.md)
    + [Namespaces](getting_started.md#namespace)
    + [Waiting for initialisation](getting_started.md#waiting-for-initialisation)
    + [New in v4.0.0](getting_started.md#new-in-v400)

### Controlling Csound ###

  * [From Unity scripts](controlling_csound_from_unity.md)
    + [Csound's channel system](controlling_csound_from_unity.md#csounds-channel-system)
    + [Starting / Stopping Instruments](controlling_csound_from_unity.md#starting--stopping-instruments)
    + [Lifecycle API](lifecycle.md)
  * [From the Unity Editor](controlling_csound_from_the_unity_editor.md)
    + [The Settings foldout](inspector_settings.md)
    + [Audio Rates: sr, kr, and ksmps](controlling_csound_from_the_unity_editor.md#audio-rates-sr-kr-and-ksmps)
    + [Audio Monitor](audio_monitor.md)
  * [From the Timeline](timelines.md)
  * [UI Components](ui_components.md)
  * [Presets](presets.md)
  * [CsoundUnityChild](csoundunity_child.md)

### Audio ###

  * [Audio Output Path](audio_output_path.md)
    + [IAudioGenerator (Unity 6+)](iaudiogenerator.md)
    + [RootOutput (Unity 6+)](root_output.md)
  * [Audio Input Routing](audio_input_routing.md)
  * [Native Audio Input](native_audio_input.md)
  * [MIDI Input](midi.md)

### Files and environment ###

  * [Loading external files](loading_external_files.md)
    + [File types](loading_external_files.md#file-types)
    + [Where Unity stores files](loading_external_files.md#where-unity-stores-files)
    + [Delivery mechanisms](loading_external_files.md#delivery-mechanisms)
    + [Sample rate considerations](loading_external_files.md#sample-rate-considerations)
  * [Environment Variables](environment_variables.md)
  * [CsoundFileWatcher](csound_file_watcher.md)

### Platforms ###

  * [Supported Platforms](platforms.md)
    + [WebGL](webgl_support.md)

### Reference ###

  * [Samples](samples.md)
    + [Importing samples](samples.md#importing-samples)
    + [Sample categories](samples.md#sample-categories)
    + [Input System compatibility](samples.md#input-system-compatibility)
  * [Migration guide: 3.x → 4.0](migration_3_to_4.md)

### API Reference ###

  * [Generated class reference](html/index.html ':ignore') — every public type, property and method
