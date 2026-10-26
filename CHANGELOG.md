## [4.0.0-alpha.2] - unreleased

- [Add] **CsoundUnityDoppler**: Unity's Doppler cannot shift what Csound synthesises, since it works by resampling a clip, so this component sends the distance, the closing speed and a frequency ratio into channels and the orchestra makes the shift itself. With a delay line it also gets the propagation delay, which Unity never had
- [Fix] Docs: `dopplerLevel` is left alone in the CsoundUnityChild example. Unity's Doppler shifts a pitch by resampling an AudioClip, and CsoundUnity writes its audio in the filter callback, so the level does nothing to what Csound synthesises, on either audio path. With **Process Audio Clip** ON it does reach the clip going into Csound
- [Add] **AudioOutputRecorder**: put it next to the AudioListener and it writes what Unity finally plays into a WAV or an AIFF, every channel of it, in a player too. It can ask the mixer for a wider speaker mode than the machine has, which is how a 5.1 mix is recorded on a stereo laptop, it reports the peak and the RMS of each channel by name, and it names the blocks that arrived late, which is the click you hear and cannot find in the file
- [Add] Sending MIDI to a csd that never asked Csound for a MIDI device prints a warning, once per instance. Csound collects what the host queues only when the csd has `-M` in its `<CsOptions>`, so without it every `SendMidiNoteOn` and the rest were queued and never read, with nothing to see and nothing to hear
- [Add] **Scan On External Save**, in the inspector's Settings: reading a csd's channels means running it, and Csound can take the editor down from inside an orchestra that compiles, so a save made in another editor no longer has to start that. Assigning a csd and the refresh button still scan. Two scans never run at once either, whichever instance asked
- [Change] **Loud Volume Warning** guards all three audio paths and its threshold is now a ceiling: a sample louder than it, either way it swings, is clamped to it rather than silenced, so a runaway comes out as a clipped version of itself instead of a waveform full of holes. A sample that is not a number becomes silence whatever the threshold is set to. The warning names the instance and the level it reached, is printed on the main thread rather than from the audio callback, and the inspector shows it while it is happening, because a clamp sounds like a loud mix rather than like a fault
- [Fix] CsoundUnityChild maps its channels one to one to Unity's output on both of its paths: the speakers past its channel count are silent instead of repeating its last channel, and a spatialized component is placed as one point source. Its two paths used to disagree in every configuration, and a MONO component on IAudioGenerator with a spatialized AudioSource reached six times full scale on a 5.1 output
- [Change] A MONO CsoundUnityChild plays at the level the parent gives the same signal: the half-level gain is gone and it reaches every speaker. A project mixed against the old level plays louder, so turn that AudioSource's volume down
- [Fix] OnAudioFilterRead maps Csound's channels one to one to Unity's output, and a spatialized instance is placed as one point source: the speakers past `nchnls` no longer carry a copy of Csound's last channel, so a stereo orchestra behind the listener is heard with both of them
- [Change] An instance on RootOutput with `processClipAudio` enabled now falls back to `OnAudioFilterRead` and prints why, as IAudioGenerator already did. Clip audio is only read in that callback, and RootOutput skips it: the clip used to reach neither Csound nor a warning
- [Fix] A csd that declares no nchnls is treated as mono, which is what Csound does, instead of storing zero channels: the route graph and the audio route drawer had no output channels to offer for it
- [Add] A preset records the **asset GUID** of the csd it was saved from, so a preset meant for another csd with the same file name is refused instead of writing its values onto channels that mean something else. Presets saved before this, and csds loaded from a string, carry no GUID and are matched by name as before
- [Add] A preset's name can be edited from its inspector, and the asset is renamed with it. When the two have already drifted apart the inspector says so, and offers to settle it either way
- [Add] Applying a preset whose values fall outside the range the csd declares now warns, naming every channel that is out. The values are applied as they are, because a preset carries what was saved: clamping it would alter that data, and lose it if the range widens again. Reported once per preset and channel, and the inspector marks those channels, warning that editing the field clamps the value and loses it
- [Add] `EnvType.CS_USER_PLUGINDIR` and `EnvType.CS_UDO_DIR`: where Csound looks for your own plugin opcodes and your UDOs, separately from the opcode directory the package sets for itself
- [Change] The Csound Test sample is now **Read Environment Paths**, under Environment: it lists the instance's own environment settings next to what Csound answers for each, in the scene and in the console. It carries no csd, because those paths come from the settings and not from an orchestra
- [Add] NativeAudioInput: the input device is chosen from the machine's own list, read in the inspector before play, with the system's own at the top and chosen by default, so a scene opens the microphone its user has selected. A position the list no longer reaches says so instead of resolving to whatever sits there
- [Add] NativeAudioInput: the device's sample rate is chosen from the rates it declares, with Follow Csound first; on Apple platforms the device is set to the one chosen and put back on close
- [Add] NativeAudioInput (Windows): the inputs past the stereo pair Windows exposes are reachable in exclusive mode. An interface that shows a 2-channel endpoint while granting 4 to the driver now reports 4 and opens them; shared mode still gets the engine's layout, and says so in the console
- [Add] CsoundUnityNativeAudioInputMonitor: the rates the open device declares, as a row that changes it while the scene plays (macOS)
- [Add] TableLoader sets the clip's own sample rate on a channel named `table<number>Rate`, so an orchestra can work out how long a table lasts instead of dividing by its own `sr`. A table carries no rate, so the two answers differ whenever the clip was not recorded at the project's rate: the Partikkel, AudioClip Reader and Simple Sequencer samples read the channel and now play at the pitch of the file
- [Add] Settings foldout: a ↺ beside Sample Rate (sr) reads the rate Unity is running at again and hands it to Csound. The number there is the running rate, which is what Csound is given, and not necessarily the one in Project Settings → Audio: that one takes effect when the audio system restarts
- [Change] NativeAudioInputManager is now **CsoundUnityNativeAudioInput**, and CsoundUnityNativeInputMonitor is **CsoundUnityNativeAudioInputMonitor**, matching CsoundUnityMidiInput. Scenes and prefabs keep their components, since Unity binds them by the file's GUID; code that names either type has to be updated
- [Add] SceneObjectUtils: `FindAny<T>()` and `FindAll<T>()`, the scene lookups through whichever call the running Unity offers
- [Change] Unity 6.4 imports the package without deprecation warnings; 2022.3 stays the minimum
- [Fix] Csound opens no audio device of its own, whatever a csd's CsOptions ask for. A csd carrying `-odac` was given a second device, unrelated to Unity's, whose driver then paced Csound from the audio thread; one asking for more channels than that device has failed to start altogether
- [Fix] A Csound session that compiles but fails to start says so in the console instead of passing in silence
- [Fix] A button channel no longer fires its instrument after Restart(). The csd zeroes the channel inside Csound, which the serialised copy never sees, so the stored 1 was poured back in on the next init
- [Fix] Csound no longer loads plugin opcodes from a Csound installed on the machine: on the editor and on desktop the search points at a folder that has none, and an opcode folder set in the Environment Settings is used instead when there is one. The channel scan in the inspector follows the same rule, so it sees what the running instance will see

## [4.0.0-alpha.1] - 2026-10-18

- [Add] Unity Timelines integration: Channel (Fixed/Random/RandomSmooth) and Score (Score/Swarm/Arpeggio/Euclidean/Stochastic/Chord/Pattern/Step) clips, CsoundTimelineStarter
- [Add] Timeline Sequencer UI: runtime UGUI builders for Step and Pattern modes with chip navigation, per-step popup (note/vel/dur), randomize and named presets (SequencerPreset); CsoundTimelineController exposes PlayableGraph traversal and a runtime BPM/step/pattern API
- [Add] Audio Input Routing: feed one CsoundUnity instance's audio into another's spin buffer, with cycle detection, mute toggle and per-route level. Each hop carries one DSP buffer of latency
- [Add] AudioRouteGraphWindow: interactive node/edge editor for the audio route graph across all scene instances
- [Add] IAudioGenerator audio path (Unity 6+): drives the AudioSource through CsoundUnity.Process with no carrier clip and constant latency. Opt-in; it adds a per-block `Configure` hook and a sample-accurate DSP clock on the audio thread. A mono orchestra plays from every speaker, a wider one maps one to one and the outputs past `nchnls` stay silent
- [Add] RootOutput audio path (Unity 6+): writes Csound's output into Unity's main output, bypassing the AudioMixer and needing no AudioSource. Audio Output Path is its own inspector section. A mono orchestra plays from every speaker, a wider one maps one to one and the outputs past `nchnls` stay silent
- [Add] Lifecycle API: initializeOnAwake toggle, Initialize(), Stop(), Restart()
- [Add] LoadCsdFromString: load a raw CSD from a string at runtime, with the string-based parsers ParseCsdString, ParseCsdStringForNchnls and ParseCsdStringForKsmps
- [Add] LoadCsdFromPath: load a CSD from a file at runtime. The synchronous overload takes filesystem paths and refuses StreamingAssets on Android and WebGL with an explicit error; the callback overload reads StreamingAssets and http/https URLs
- [Add] Cabbage widgets CsoundUnity cannot build a control for (listbox, gentable, signaldisplay, …) are warned about when the csd is assigned and listed above the Control Channels foldout
- [Add] CsoundUnityMidiInput: platform-agnostic MIDI input (macOS/iOS/visionOS CoreMIDI, Android android.media.midi API 23+, Windows WinMM — short messages only)
- [Add] Waveform, spectrum, spectrogram, oscilloscope and Lissajous monitors in the inspector, with zoom sliders; the selection persists across selections and restarts
- [Add] OutputBuffer and the OnCsoundPerformKsmps callback: OutputBuffer hands back a complete DSP block, safe to poll from the main thread at any rate, returning the previous block when no new one has been produced. CsoundUnityChild has its own OutputBuffer / OutputChannels, interleaved and in Unity's ±1 range — prefer them to namedAudioChannelData
- [Add] GetAudioChannelReader(channel): reads the audio a named channel publishes, one finished block at a time, from any thread. Each reader keeps its own position in the stream, so a meter, a visualiser and a gameplay script on the same channel all receive every block
- [Add] MusicUtils: scales, chords, arpeggios and Euclidean rhythms
- [Add] Utility scripts: AudioDisplay, FFTUtils, TableLoader, WriteAudioFileUtils, CopyFilesToPersistentDataPath, RemapUtils
- [Add] AudioSamplesUtils.Rms() and Peak()
- [Add] CsoundUnityVectorMorph: bilinear blending between four CsoundUnityPresets, with an interactive editor
- [Add] Copy / Paste of control channels through the system clipboard, from the Control Channels context menu or with CopyChannelsToClipboard() / PasteChannelsFromClipboard() at runtime. A clipboard snapshot and a .json preset use the same format; pasting from a different csd applies the channels whose names match and reports the rest
- [Add] UI components with prefabs: CsoundUnitySlider (hslider/vslider), CsoundUnityKnob (rslider), CsoundUnityEncoder (encoder), CsoundUnityButton, CsoundUnityToggle, CsoundUnityDropdown, CsoundUnityXYPad, CsoundUnityKeyboard, CsoundUnityLabel, CsoundUnityMeter, CsoundUnityNSlider, CsoundUnityPianoKey, CsoundUnityRangeSlider. They follow their channel while the scene runs, including when a preset is applied or channels are pasted
- [Add] UI auto-layout: **Create UI** and **Update UI** build a Canvas from Cabbage bounds; `CsoundUnityUISettings` maps widget types to prefabs and sets font scale
- [Add] NativeAudioInput: low-latency multichannel input through each platform's own API — CoreAudio/AUHAL, AudioUnit, AAudio exclusive (4 ms @ 48 kHz) and WASAPI. Captured frames reach Csound's spin buffer every ksmps, so `inch` reads them. Device, channel count and capture reserve size themselves, and every one of them can be pinned by hand
- [Add] WebGLAudioInput: routes the browser microphone (`getUserMedia`) into Csound behind the same Open/Close API as NativeAudioInputManager, up to 2 channels
- [Add] WebGLMidiReceiver, and CsoundUnityMidiInput on WebGL, through the Web MIDI API; needs HTTPS, Chrome or Edge
- [Add] CsoundUnityNativeInputMonitor: on-screen panel for a native input that stays silent — which device is open, whether anything is arriving, an input meter, and a device list that switches without leaving play mode. Needs no Canvas or EventSystem
- [Add] CsoundUnityAudioInputRouter: enables NativeAudioInputManager in the Editor and in standalone builds and WebGLAudioInput in WebGL builds, so one scene covers every target
- [Add] WebGL: CompileOrc, SendScoreEvent and SetStringChannel are implemented
- [Add] WebGL: `-odac` is set by default when the csd declares no output, `-iadc` when it declares input channels, and `-n` is dropped from the compiled copy so a csd written for the native input path also runs in the browser
- [Add] WebGL Spatialization, a per instance toggle: with it on, the position is sent to the csd as the channels azimuth, elevation and rolloff once a frame, which is how a source is placed where Csound plays through the browser rather than through Unity. Off for a csd that does not read them, or that uses those names for something else
- [Add] CsoundUnity.IsWaitingForUserGesture: true while a browser withholds audio until the page is clicked, false on every other platform, so a "click to start" notice needs no platform check
- [Add] WebGL docs: the user gesture Csound needs before it starts
- [Add] Cabbage parser: encoder widget (channel, increment, value, text/popupPrefix) and form widget (size → canvas dimensions)
- [Add] Two-value Cabbage widgets — xypad, hrange and vrange — in the parser, the inspector, the preset editor, and in preset morphing and blending
- [Add] SampleInputSystemFixer: patches imported sample scenes for Unity's new Input System
- [Add] CSD refresh button and CREATE from template button in the inspector
- [Add] Many new samples across new and existing categories: Timelines, UI, Samplers, Collisions, Miscellaneous, Presets, Engines
- [Add] **GameObject > CsoundUnity** holds everything the package creates: the CsoundUnity and CsoundUnity Child objects, and the UI widgets below them
- [Add] **GameObject > CsoundUnity > UI** creates any of the UI widgets, making the Canvas and the EventSystem when the scene has none. A selected CsoundUnity gets the Canvas as its child, so the widget finds its instance without a field being assigned
- [Add] OnCsoundStopped and OnCsoundPerformanceFinished events
- [Add] pauseProcessing: silences the output and stops Csound performing, at no DSP cost. The score freezes, so clearing the flag resumes from where it stopped, and nothing is torn down
- [Add] `fadeInSeconds` on CsoundUnity and CsoundUnityChild ramps the output up from silence on start, masking the transient of chained sources. Honoured by every audio path, and 0 by default
- [Change] Minimum Unity version is 2022.3 LTS
- [Change] **mute means silence, not freeze.** The output is silenced while Csound keeps performing, so the score stays in time and unmuting drops back in on the beat; audio input routes, native audio input and clip audio keep flowing in. Use pauseProcessing to freeze instead
- [Change] **OnAudioFilterRead plays at full level.** The carrier clip matches the output width, so this path measures the same as the csd rendered outside Unity and the same as the other two paths. A project mixed against the old level plays 3 dB louder: turn the AudioSource volume down by 3 dB
- [Change] **The audio path of a component that has never been configured is OnAudioFilterRead**, on CsoundUnity and CsoundUnityChild alike, which covers 3.x scenes and components created at runtime with AddComponent. Both Unity 6 paths are one dropdown away
- [Change] **CsoundUnityChild no longer forces its AudioSource to 3D on every Play.** `spatialBlend`, `velocityUpdateMode` and `spatializePostEffects` are applied once, when the component is added or on `Init()` for a Child built by script, so later changes are honoured and 2D output is possible. A 3.x scene usually serialized `spatialBlend` 0, so those children now play 2D: set it back to 1
- [Change] **`CurrentPreset` returns the preset, not its name**, so the one picked in the inspector is applied again when Csound starts. The name is `CurrentPresetName`; a global preset sets only the name
- [Change] `CsoundUnityBridge.OnApplicationQuit()` is now `Destroy()`. Code calling `bridge.OnApplicationQuit()` must call `bridge.Destroy()`
- [Change] `EnvType` uses the Csound 7 names: `OPCODE7DIR`, `OPCODE7DIR64`, `CSOUND7RC`. Scenes and presets are unaffected, code naming a member is not
- [Update] **Csound 7.0** on macOS, Windows, iOS, Android and WebGL. visionOS stays on Csound 6
- [Update] CsoundCsharp.cs and CsoundUnityBridge.cs follow the Csound 7 API (breaking changes in csoundCreate, csoundCompileOrc, csoundCompileCSD, csoundEventString, csoundGetChannels and others)
- [Update] Inspector: sr/kr/ksmps redesigned with a single override toggle
- [Update] CsoundUnitySlider applies skew and increment from its ChannelController
- [Update] Presets: Load and Save folders default to a Presets folder beside the CSD, with a "Next to Csd" shortcut, "All To JSON" bulk convert, tooltips, and a list that shrinks to fit
- [Fix] WebGL: Csound holds its start until the page's first click, tap or key press. Starting into a suspended audio context made the start promise time out and resolve with no node, which left the instance mute and without a microphone for the whole session, and nothing retried it
- [Fix] WebGL: the control channels are set before the first performance pass, so an instrument that reads one at init time, as i-rate chnget does, sees the value the inspector holds instead of zero
- [Fix] WebGL: a file in the assets list that the server does not have is skipped with a warning. The failed request's error page used to be written under the name the csd expects, so the opcode reading it reported something unrelated instead of a missing file
- [Fix] WebGL sample: BinauralTest.csd glides the elevation and the rolloff it receives. A host sends them once a frame, and a gain applied in steps is zipper noise: small interruptions that sound like a fault in the audio path and are a missing line in the csd
- [Fix] WebGL sample: BinauralTest.csd declares two output channels. With one, Csound kept the left ear of the HRTF pair and dropped the right, saying so only as "Excess channels ignored": the sources still changed with direction, since one ear carries that too, but without the difference between the ears that places them left or right
- [Fix] Every teardown route frees the native Csound instance exactly once — Stop, OnDisable, OnDestroy, scene change and quit alike
- [Fix] Teardown shuts the bridge to the audio thread and waits for any in-flight callback to leave before freeing, on all three audio paths. A teardown asked for from inside an audio callback is refused with an explanation
- [Fix] The carrier AudioClip is stopped with the instance and paused while the component is disabled, on CsoundUnityChild too, and every path that returns without writing a block zeroes it: a stopped, finished or csd-less component no longer puts full-scale DC on the output
- [Fix] CsoundUnityChild reads the completed blocks its parent publishes at the end of each DSP period, so it cannot pick up a half-written block and falls silent when its parent stops
- [Fix] A change of audio device rebuilds the carrier AudioClip on CsoundUnity and CsoundUnityChild instead of leaving the instance silent. A device at a different sample rate is reported, and `Restart()` picks the new rate up
- [Fix] A change of DSP buffer size resizes the per-channel buffers, so routed destinations and CsoundUnityChild are no longer handed a short block padded with silence
- [Fix] With Domain Reload disabled a finished score no longer stays finished into the next Play session
- [Fix] The channel name→index map is rebuilt on demand, so SetChannel and GetChannelController keep working after a script recompile
- [Fix] An AudioClip assigned to the AudioSource while Process Audio Clip is off is ignored with a warning instead of being multiplied into Csound's output; turning Process Audio Clip on with no clip assigned warns as well
- [Fix] Selecting a CsoundUnityChild no longer logs NullReferenceException from Unity's built-in audio filter meter
- [Fix] Multi-object editing is disabled in the inspector, instead of overwriting every selected component with the first one's csd, score, settings and channel values
- [Fix] Applying a preset sets values only — the channel definition stays the csd's, and the preset carries the value plus the second value of an xypad or range widget
- [Fix] Presets: combobox channels apply the right option, and a saved preset's stale options no longer overwrite the current csd's. **A csd that added 1 to a combobox channel to work around the old behaviour has to drop that compensation**; the Dr B. Samplers csds are updated
- [Fix] Presets: AssetDatabase.ImportAsset crash on JSON save, null checks in SetPreset/UpdateAssignablePresets, "To JSON" saves alongside the SO asset by default, JSON list filtered to the current CSD
- [Fix] Presets: folder pickers keep the folder that was picked, open at the right one, and no longer clear it on cancel
- [Fix] Cabbage parser: caption/text truncation with several quoted attributes on one line, and whitespace before '(' not recognised
- [Fix] Cabbage parser: `_channelsIndexDict` index when a form widget is at position 0, and stale entries after a Domain Reload
- [Fix] Table API: `GetTableArgs` no longer frees memory owned by Csound and returns -1 for a table that does not exist; `TableCopyIn` no longer reads past the end of its buffer
- [Fix] CsoundFileWatcher handles atomic saves from modern editors
- [Fix] Hang on exit: an end-score event is sent before csoundDestroy, so indefinitely-running instruments stop cleanly
- [Fix] BasicMicrophoneAnalyzer sample: waits for `Microphone.GetPosition > 0` before `AudioSource.Play()` and uses `AudioSettings.outputSampleRate` as the capture rate, so the mic clip no longer produces zeros for the lifetime of the scene
- [Fix] Partikkel sample: the parameter cubes no longer log a MissingReferenceException every frame once the scene is left

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
