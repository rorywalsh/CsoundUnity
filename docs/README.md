# CsoundUnity: Procedural audio for Unity #

**Documentation for version 4.0.0.**

3.x docs available [here](https://rorywalsh.github.io/CsoundUnity/v3/).

CsoundUnity embeds the Csound audio engine in Unity. Add a CsoundUnity component to a GameObject, assign a
`.csd`, and control the sound from C# or Timeline. The package includes the Csound runtime.

* **Real-time synthesis and sample processing.** Generate sounds from gameplay data, such as
  engine pitch driven by RPM. You can also play and process recordings, use SoundFonts, and
  combine samples with synthesis. See [Loading external files](loading_external_files.md).
* **Deep Timeline integration.** Clips that fire score events, clips that automate a channel, and
  sequencers on the same track: patterns, step sequencer, arpeggio, euclidean, chord, stochastic
  and swarm.
* **Native audio input.** Capture audio from supported devices for live processing. See
  [Native Audio Input](native_audio_input.md) for backends and platform requirements.
* **MIDI in** from keyboards and controllers, straight to the orchestra.
* **Spatial audio.** On the AudioSource output paths, use Unity’s panning, attenuation, and mixer
  routing. [Procedural Doppler](doppler.md) can model pitch shift and propagation delay in Csound.
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
* **Csound 7 on desktop, mobile, and web.** See [Supported Platforms](platforms.md) for backend
  availability, minimum requirements, and experimental targets.
* **Samples for every part of it**, each a scene you can open and play. See [Samples](samples.md).

### Daily project spotlight

<div id="daily-project" aria-live="polite"></div>

[Browse all projects and add yours](PROJECTS.md)

### Paper of the day

<div id="daily-paper" aria-live="polite"></div>

[Browse all papers and theses](PAPERS.md)

---

### Get started ###

  * [How to import CsoundUnity](importing.md)
  * [Quick Start](quick_start.md) — from nothing to a sound you can change, in one page
  * [Coming from Unity](coming_from_unity.md) — for Unity developers meeting Csound for the first time
  * [Coming from Csound](coming_from_csound.md) — for Csound users meeting a game engine for the first time
  * [Coming from Wwise or FMOD](coming_from_middleware.md): for sound designers who already ship games with middleware
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
