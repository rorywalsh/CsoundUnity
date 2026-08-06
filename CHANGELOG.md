## [4.0.0]

- [Add] Unity Timelines integration: Channel (Fixed/Random/RandomSmooth) and Score (Score/Swarm/Arpeggio/Euclidean/Stochastic/Chord/Pattern/Step) clips, CsoundTimelineStarter
- [Add] Timeline Sequencer UI: runtime UGUI builders for Step and Pattern sequencer modes (StepUIBuilder, PatternUIBuilder, SequencerUIBase); chip navigation, per-step popup (note/vel/dur), randomize, named presets (SequencerPreset ScriptableObject); CsoundTimelineController for PlayableGraph traversal and runtime BPM/step/pattern API
- [Add] Audio Input Routing: connect CsoundUnity instances to feed audio into another instance's spin buffer, with cycle detection, mute toggle and per-route level control. Each hop carries one DSP buffer of latency; a muted source feeds silence to its destinations
- [Add] AudioRouteGraphWindow: interactive node/edge editor to visualise and edit the audio route graph across all scene instances
- [Add] IAudioGenerator audio path for Unity 6+: drives the AudioSource directly via CsoundUnity.Process, set as default on Unity 6 (OnAudioFilterRead remains available)
- [Add] RootOutput audio path for Unity 6+: writes Csound output directly into Unity's main audio output via RootOutputInstance, bypassing the AudioMixer and requiring no AudioSource; Audio Output Path is now a dedicated inspector section
- [Add] Lifecycle API: initializeOnAwake toggle, Initialize(), Stop(), Restart()
- [Add] LoadCsdFromString: load a raw CSD from a string at runtime without a `.csd` asset (runtime counterpart of the editor-only SetCsd); string-based parsers ParseCsdString/ParseCsdStringForNchnls/ParseCsdStringForKsmps
- [Add] CsoundUnityMidiInput: platform-agnostic MIDI input component (macOS/iOS/visionOS via CoreMIDI, Android via android.media.midi API 23+, Windows via WinMM — short messages only, no SysEx)
- [Add] Waveform, spectrum, spectrogram, oscilloscope and Lissajous audio monitor in inspector, with zoom sliders. The views draw in play mode; which ones are shown can be set at any time and persists across selections and restarts
- [Add] OutputBuffer and OnCsoundPerformKsmps callback. OutputBuffer hands back a complete DSP block, safe to poll from the main thread at any rate; when no new block has been produced the previous contents are returned unchanged
- [Add] MusicUtils: music theory utilities (scales, chords, arpeggios, Euclidean rhythms)
- [Add] Utility scripts: AudioDisplay, FFTUtils, TableLoader, WriteAudioFileUtils, CopyFilesToPersistentDataPath, RemapUtils
- [Add] AudioSamplesUtils.Rms() and Peak() helpers
- [Add] CsoundUnityVectorMorph: bilinear blending between four CsoundUnityPresets with interactive editor
- [Add] UI components: CsoundUnitySlider (hslider/vslider), CsoundUnityKnob (rslider — bounded rotary knob), CsoundUnityEncoder (encoder — endless rotary, unbounded), CsoundUnityButton, CsoundUnityToggle, CsoundUnityDropdown, CsoundUnityXYPad, CsoundUnityKeyboard, CsoundUnityLabel, CsoundUnityMeter, CsoundUnityNSlider, CsoundUnityPianoKey, CsoundUnityRangeSlider (horizontal and vertical) with prefabs
- [Add] UI auto-layout: **Create UI** and **Update UI** buttons in the CsoundUnity inspector generate a Unity Canvas from Cabbage bounds data; `CsoundUnityUISettings` ScriptableObject maps widget types to prefabs and controls font scale
- [Add] NativeAudioInput: low-latency multichannel audio input for macOS (CoreAudio/AUHAL), Android (AAudio exclusive mode, 4ms @ 48kHz) and Windows (WASAPI shared/exclusive)
- [Add] WebGLAudioInput: optional component to route browser microphone (`getUserMedia`) into Csound on WebGL; wraps the built-in Csound WASM audio-input path behind the same Open/Close API as NativeAudioInputManager (max 2 channels — browser limitation)
- [Add] WebGLMidiReceiver / CsoundUnityMidiInput on WebGL: MIDI input via Web MIDI API (`navigator.requestMIDIAccess`); requires HTTPS, Chrome/Edge only
- [Add] CsoundUnityAudioInputRouter: component that enables NativeAudioInputManager in Editor/standalone and WebGLAudioInput in WebGL builds automatically; use it to share one scene across all targets
- [Add] Cabbage parser: encoder widget (channel, increment, value, text/popupPrefix); form widget (size → canvas dimensions)
- [Add] SampleInputSystemFixer: editor script that automatically patches imported sample scenes for Unity's new Input System
- [Add] xypad Cabbage widget support in parser and inspector
- [Add] CSD refresh button and CREATE from template button in inspector
- [Add] Many new samples across new and existing categories: Timelines, UI, Samplers, Collisions, Miscellaneous, Presets, Engines
- [Add] Context menu shortcuts to quickly create CsoundUnity GameObjects
- [Add] OnCsoundStopped and OnCsoundPerformanceFinished events
- [Update] Csound native libraries updated to **Csound 7.0** across all platforms (macOS, Windows, iOS, visionOS, Android)
- [Update] CsoundCsharp.cs and CsoundUnityBridge.cs updated for the Csound 7 API (breaking changes: csoundCreate, csoundCompileOrc, csoundCompileCSD, csoundEventString, csoundGetChannels and others)
- [Update] Inspector: sr/kr/ksmps redesign with single override toggle
- [Update] CsoundUnitySlider now applies skew (logarithmic/exponential mapping) and increment (stepped values) from ChannelController
- [Add] pauseProcessing: silences the output **and** stops Csound performing, with no DSP cost. The score freezes, so clearing the flag resumes from exactly where it stopped; unlike disabling the GameObject or calling Stop() nothing is torn down. This is the behaviour `mute` used to have
- [Fix] CsoundUnityChild could click, and kept sounding after its parent was silenced: it read the parent's live working buffer from its own audio callback, and Unity defines no ordering between the two, so it could pick up a half-written block — and on 32-bit ARM a torn sample. A parent that stopped updating that buffer left the child looping its last block instead of falling silent. It now reads the completed blocks the parent publishes at the end of each DSP period
- [Change] **mute now means silence, not freeze.** It silences the output while Csound keeps performing, so the score stays in time and unmuting drops back in on the beat instead of resuming where it left off; inputs (audio input routes, native audio input, clip audio) keep flowing in. Previously muting skipped PerformKsmps entirely and froze the score. Use the new pauseProcessing for the old behaviour
- [Fix] Selecting more than one CsoundUnity at a time overwrote all of them with the first one's csd, score, settings and channel values, as soon as the inspector drew. Multi-object editing is disabled until every field handles it correctly
- [Change] **CsoundUnityChild no longer forces its AudioSource to 3D on every Play.** `spatialBlend`, `velocityUpdateMode` and `spatializePostEffects` are applied once — when the component is added in the inspector, or on `Init()` for a Child built by script — so the values you set afterwards are honoured. 2D output is a legitimate use of a Child and was impossible before. Scenes made with 3.x kept whatever `spatialBlend` was serialized, usually 0 because setting it had no visible effect, so those children will now play 2D: set it back to 1 on the AudioSource
- [Fix] CsoundFileWatcher: handle atomic saves from modern editors
- [Fix] Presets: AssetDatabase.ImportAsset crash on JSON save, null checks in SetPreset/UpdateAssignablePresets, "To JSON" now saves alongside the SO asset by default, JSON list filtered to current CSD
- [Fix] Presets: combobox channels applied one option too low (Cabbage index is 1-based) and stale combobox options in a saved preset overwrote the current CSD's option set
- [Fix] Presets: folder pickers could clear the folder on cancel, open at the wrong folder, or fail to keep the one picked
- [Update] Presets: Load and Save folders default to a Presets folder beside the CSD; adds a "Next to Csd" shortcut, "All To JSON" bulk convert, tooltips, and a preset list that shrinks to fit
- [Fix] Cabbage parser: caption/text truncation with multiple quoted attributes on same line; whitespace before '(' not recognised
- [Fix] _channelsIndexDict wrong index when form widget is at position 0; stale entries after Domain Reload
- [Fix] Hang on exit: send end-score event before csoundDestroy to stop indefinitely-running instruments cleanly
- [Fix] BasicMicrophoneAnalyzer sample: now waits for `Microphone.GetPosition > 0` before `AudioSource.Play()`, uses `AudioSettings.outputSampleRate` for the capture rate; corrects an issue where the mic clip produced zeros for the lifetime of the scene

## [3.5.2] - 2025-04-28

- [Fix] Version bump

## [3.5.1] - 2025-04-15

- [Add] Support for visionOS

## [3.5.0] - 2024-08-10

- [Add] WebGL support (experimental)
- [Add] Ability to set sampling rate and control rate
- [Fix] Got rid of null refs errors on editor when going into play mode

## [3.4.3] - 2024-04-26

- [Fix] Unity crash on macOS
- [Fix] Couldn't set a global preset in editor
- [Remove] Commented section when searching for new presets. Apparently useless, saving resources
- [Remove] Useless call to SaveAssets after creating a new preset
- [Fix] Avoid calling AssetDatabase.Refresh() when presets are saved

## [3.4.2] - 2024-01-20

- [Update] Android libraries to Csound 6.19 beta, to fix sound font issues

## [3.4.1] - 2023-10-21

This release is a collection of hot fixes made in the last year, some of those were already on the master branch for quite some time.

- [Fix] Android crash in Unity versions above 2021.3.27  
- [Fix] Unity Editor crashing on Windows if having a different version of Csound installed  
- [Fix] Apps targeting Android ARM7 devices now build and run correctly  
- [Fix] iOS builds correctly  
- [Fix] Drag&Drop issue on Windows editor  

## [3.4.0] - 2022-10-30

- added CsoundUnityPresets!
- added Plugin EnvironmentSetting to be able to LoadPlugins on Android
- added LoadPlugins method
- added the possibility to edit multiple CsoundUnity components at once
- added skew and increment to ChannelControllers for future implementation on Editor sliders
- added lots of utilities methods
- updated Csound libraries to Csound 6.18 (Silicon support for Mac!)
- updated GetSamples method to not require the origin
- updated Samples
- fix for CsoundUnityWatcher enabling CsoundUnity instance when it's disabled
- fix for comboboxes and controller text not being set

## [3.3.1] - 2022-04-11

- fix for LinkButton not available in Unity versions < 2021.1

## [3.3.0] - 2022-03-27

- added VU Meters to the inspector;
- better Environment Settings handling;

## [3.2.1] - 2022-03-06

- hotfixes for errors on import
- fix for links in github pages

## [3.2.0] - 2022-03-06

- added iOS support;
- updated Csound libs to version 6.17;
- added customizable EnvironmentSettings, to set Csound Environment Variables;
- added SFDIR sample;
- heavy cleaning;
- updated documentation with newest features;
- updated README;

## [3.1.1] - 2022-01-28

- updated changelog
- updated version in package.json

## [3.1.0] - 2022-01-27

- added support for Android x86_64;
- added dripwater sample;
- added Basic Collision sample;
- added Basic FM Synth sample;
- added first Table morphing sample;
- added Basic Microphone Analyzer scene;
- added Partikkel sample;
- added ProcessClipAudio sample;
- fix for crash in GetChannelList;
- fix for comboboxes channels not initialized correctly on Start;
- fix for CsoundUnity being null on CsoundUnityChild Awake;
- fix for namedAudioChannelData of CsoundUnityChild not initialized;
- fix to avoid setting a dummy clip when processClipAudio is selected;

## [3.0.1] - 2021-09-14

- Spatialization issue fix
- Some little changes in how CsoundUnityChild is initialized, to be able to create children from code
- Small editor fixes for Cabbage buttons
- Removed csd content from logs
- CsoundFileWatcher enabled by default

## [3.0.0] - 2021-05-03

## Release of version 3.0 ##

**Changes from 2.3**

**New Features**
- Restructured the code in the form of a Unity Package (https://docs.unity3d.com/Manual/PackagesList.html).
- Updated libraries to **Csound 6.15**.
- Implemented most of the Csound API.
- Several improvements of the Editor inspector: 
	- Now the changes made to the *Control Channels* found in the csd are correctly serialised and saved in the Scene, and are fully compatible with Unity Inspector presets.
	- Added Edit Csd Section, to be able to edit the csd from Unity, and save its content on disk.
	- Added Test Score Section, to be able to send score to Unity when testing in Editor.
	- Added **AudioChannels**: the csd file is scanned for *chnseta* opcodes, and the resulting Audio Channels can be seen in the inspector, and publicly accessed from a dictionary.
	- Added folded groups:
		- Settings
		- Edit Csd Section
		- Test Score Section
		- Control Channels
		- AudioChannels
- Added **CsoundUnityChild**, to be able to read audio from the AudioChannels of a CsoundUnity instance.
- Added **CsoundFileWatcher** to detect changes made by an external program to the csds used in the scene, and update them. Add *FILEWATCHER_ON* in your project *Scripting Define Symbols*.
- Added Android libraries, builds working on **Android 64bit**.
- Added an utility method to load samples from AudioClips (currently from Resources folder only).
- Added CreateTable methods, to be able to create Csound tables from float arrays.
- Added a toggle to show warnings and hard filtering output samples with values higher than a threshold.
- **Cabbage widgets**: Added support for *ComboBoxes*.
- Added IsInitialized property and OnCsoundInitialized event.
- Added PerformanceFinished property.
- Added Samples: *Csound Test*, *Basic Test*, *CsoundUnityChild Test*, *Simple Sequencer*, *AudioClip Reader*.
- Overriding the sample rate and control rate of the csd using Unity Audio Project Settings.
- Improved Logging.
- Updated **macOS library** to use a .bundle instead of a .framework, to be able to build for macOS straight out of the box.
- Totally removed the need to use the StreamingAssets folder to store csds, libraries and audio files, the csd is saved into the CsoundUnity instance as soon as it is dragged in the *Csd Asset* inspector field.
- Removed the overwriting of the *Path* variable of the operating system.

**Fixes**
- Crash on exit.
- Fixes for distorted audio output when reading mono files.
- Build/Editor *DLLNotFound* issues on macOS.

## [3.0.0] - 2020-04-07

Started development!
