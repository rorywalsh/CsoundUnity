## [3.0.0] - 2020-04-07

Started development!

## [3.0.0] - 2021-05-03

## Release of version 3.0 ##

**Changes from 2.3**

**New Features**
- Restructured the code in the form of a Unity Package (https://docs.unity3d.com/Manual/PackagesList.html).
- Updated libraries to **Csound 6.15**.
- Implemented most of the Csound API.
- Several improvements of the Editor inspector: 
	- Now the changes made in the csd's *Control Channels* are correctly serialised and saved in the Scene, and are fully compatible with Unity Inspector presets.
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
