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
