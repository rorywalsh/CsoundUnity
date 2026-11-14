## Coming from Wwise or FMOD

Use this workflow to give a vehicle a continuous engine sound and a separate sound for each impact.
You can run the same setup on two vehicles and control them independently. For your first
`.csd` and CsoundUnity component, start with the [Quick Start](quick_start.md).

### Decide what each part of the project owns

| Task | Where to do it |
| --- | --- |
| Design the engine tone, choose how it responds to the throttle, or layer a recording over it | In the Csound instruments in your `.csd` |
| Supply throttle values and decide when a collision should make a sound | In the vehicle’s gameplay code |
| Position each vehicle’s audio and route it to the vehicle bus | On its Unity AudioSource and in the AudioMixer, using the default output path on supported targets |

A Wwise Game Parameter or FMOD parameter is a useful analogy for a Csound **control channel**.
Here, the channel supplies a value to the orchestra. The Csound instrument uses that value to
set frequency, gain, or any other synthesis parameter. It can also smooth changes before they
reach the audio signal.

### Set up two vehicle emitters

Add a CsoundUnity component to each vehicle and assign the same `.csd` to both. Each component
runs its own Csound instance and has its own channel values. On the default output path, set up
each AudioSource for 3D sound and route both AudioSources to the same AudioMixer group.

This complete `.csd` uses a simple oscillator for the engine and a short noise burst for impacts.
It stays active for the duration of the scene.

```csound
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1
    kThrottle chnget "throttle"
    kThrottle limit kThrottle, 0, 1
    kSmooth portk kThrottle, 0.03
    aDrive interp kSmooth
    aEngine oscili 0.15 * aDrive, 45 + 100 * kSmooth
    out aEngine, aEngine
endin

instr 2
    iLevel limit p4, 0, 1
    aEnvelope linseg 0, 0.005, 1, p3 - 0.005, 0
    aNoise rand 0.2 * iLevel
    aImpact tone aNoise, 1400
    aHit = aImpact * aEnvelope
    out aHit, aHit
endin
</CsInstruments>
<CsScore>
f0 z
i 1 0 z
</CsScore>
</CsoundSynthesizer>
```

In your vehicle script, keep a reference to that vehicle’s CsoundUnity component as `csound`.
While the vehicle is running, send its normalized throttle, including zero when released.
The example maps it to pitch and gain in Csound. The smoothing happens there too.

```csharp
if (csound.IsInitialized)
    csound.SetChannel("throttle", Mathf.Clamp01(throttle));
```

When your collision logic accepts an impact, send a score event to that same component. The
event starts instrument 2 immediately, runs it for 0.15 seconds, and passes the normalized
impact strength as its first extra parameter, `p4`. Use invariant formatting so decimal points
stay the same across system locales.

```csharp
if (csound.IsInitialized)
    csound.SendScoreEvent(FormattableString.Invariant($"i 2 0 0.15 {Mathf.Clamp01(impactStrength)}"));
```

These snippets assume `using UnityEngine;` and `using System;` in your script. Calls made
before Csound initializes are dropped. Use [the lifecycle API](lifecycle.md) if you need to send
initial values as soon as an instance becomes ready.

To audition the impact before wiring up collisions, enter **Play Mode** and use **Test Score
Section → Send score** in the CsoundUnity Inspector with `i 2 0 0.15 0.7`. For a complete
collision-driven scene, import **Collisions** and open **Dripwater**. Each impact sends a score
event with parameters derived from the contact position.

### Use recordings and route the result

The same impact event can play a WAV or pick from several samples. Put sample selection,
layering, and processing in the Csound instrument. Use
[Loading external files](loading_external_files.md) to choose how your target loads audio files
and SoundFonts. If you already have a Unity AudioClip, the
[AudioClip Reader sample](samples.md) shows how to copy it into a Csound table.

Use an [AudioMixer snapshot](audio_output_path.md#the-three-paths) for a mix state and a
[CsoundUnity preset](presets.md) for the instrument’s control values. Those affect different
parts of the signal chain. Check [Audio Output Path](audio_output_path.md) before choosing
`RootOutput`, which bypasses the AudioMixer, or targeting WebGL, which sends audio directly to
the browser.

### Plan the behaviours your project needs

- **Voice lifetime.** A score event starts a note with its own duration. `SendScoreEvent` returns
  no playback handle. For a held note you need to stop individually, give it a unique fractional
  instrument number and send a matching negative number to turn it off. See
  [Starting and stopping instruments](controlling_csound_from_unity.md#starting--stopping-instruments).
- **Voice limits and spatial behavior.** Put cooldowns and any shared voice budget in gameplay
  code. Use Csound’s `maxalloc` to cap concurrent notes of one instrument within an instance.
  For several independently positioned outputs from one orchestra, use
  [CsoundUnityChild](csoundunity_child.md) with named audio channels.
- **Music scheduling.** [Timeline](timelines.md) can trigger score events and automate channels.
  Choose a scheduling mode based on whether you need tight pattern timing or tempo changes
  within the pattern. Put interactive transitions such as combat entry and exit in your game
  or Timeline logic.
- **Iteration and shipping.** Audition and inspect the running scene with
  [Audio Monitor](audio_monitor.md). [CsoundFileWatcher](csound_file_watcher.md) picks up
  external edits in the Unity Editor. Use [the file-loading guide](loading_external_files.md)
  to plan which assets ship and how each target accesses them, and verify those paths in a build.
