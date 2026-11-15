## Coming from Wwise or FMOD ##

This page is for sound designers and audio programmers who already work with events, parameters,
and buses in Wwise or FMOD. It shows where those decisions live in a CsoundUnity project. You can
follow the example with no prior Csound experience. The [Quick Start](quick_start.md) walks
through adding your first `.csd` to a scene.

---

### What you are used to ###

In Wwise or FMOD Studio, you might author an engine sound that follows a throttle parameter and
an impact sound that plays when the vehicle hits something. The game supplies the throttle value
and decides when to trigger the impact. Your audio project defines what each action sounds like
and how it reaches the mix.

### Where that work goes in CsoundUnity ###

You write the sound behaviour in a `.csd`, a text file that defines Csound instruments. An
instrument can generate sound, play a recording, or combine the two. Unity runs the file through
a CsoundUnity component. Your game updates a **control channel** to change a running sound or
sends a **score event** to start an instrument. On the default desktop audio path, the AudioSource
and AudioMixer handle placement and mixing.

The example below follows the engine and impact from gameplay to the mix, using two vehicles
that share the same `.csd` and respond independently.

---

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
{
    var strength = Mathf.Clamp01(impactStrength);
    var score = FormattableString.Invariant($"i 2 0 0.15 {strength}");
    csound.SendScoreEvent(score);
}
```

These snippets assume `using UnityEngine;` and `using System;` in your script. Calls made
before Csound initializes are dropped. Use [the lifecycle API](lifecycle.md) if you need to send
initial values as soon as an instance becomes ready.

To audition the impact before wiring up collisions, enter **Play Mode** and use **Test Score
Section → Send score** in the CsoundUnity Inspector with `i 2 0 0.15 0.7`. For a complete
collision-driven scene, import **Collisions** and open **Dripwater**. Each impact sends a score
event with parameters derived from the contact position.

### Use recordings and route the result

For an impact that plays a recorded sound, choose an `AudioClip` in C# and play it through a
Unity AudioSource. That works alongside the Csound engine sound. For example, C# can choose a
clip based on the surface the vehicle hits.

Send the recording to Csound when the sound calls for processing you want to implement there,
such as turning the impact into a granular texture or layering it with synthesized resonance.
You can still choose the clip in C#. Use [Process Audio Clip](loading_external_files.md#2-audiosource-clip--csound-spin-buffer-processclipaudio)
to process playback from a Unity AudioSource on the default audio path, or copy the clip into
a [Csound table](loading_external_files.md#1-audioclip--in-memory-function-table) for random
access to its samples.

To play an audio file from disk inside Csound, use
[CopyFilesToPersistentDataPath](environment_variables.md#copyfilestopersistentdatapath) to copy it
to `Application.persistentDataPath` before Csound starts. In the CsoundUnity component’s
Environment Settings, point `SFDIR` at the destination folder. Then use the filename with
`diskin2` in your instrument. The **Environment → SFDIR** sample shows the setup.
For browser builds, see [WebGL file loading](webgl_support.md#read-files).

Use an [AudioMixer snapshot](audio_output_path.md#the-three-paths) for a mix state and a
[CsoundUnity preset](presets.md) to store and recall the instrument’s control values. Those
affect different parts of the signal chain. Check [Audio Output Path](audio_output_path.md) before choosing
`RootOutput`, which bypasses the AudioMixer, or targeting WebGL, which sends audio directly to
the browser.

### Plan the behaviours your project needs

- **Voice lifetime.** A score event starts a note with its own duration. `SendScoreEvent` returns
  no playback handle. For a held note you need to stop individually, give it a unique fractional
  instrument number and send a matching negative number to turn it off. See
  [Starting and stopping instruments](controlling_csound_from_unity.md#starting--stopping-instruments).
  For a worked example, open **Miscellaneous → MultiTouch XY Synth**. Its script uses a fractional
  instrument number to start and release a note for each touch. It also passes the touch ID as
  `p4`, which the instrument uses to read that touch’s X and Y channels. The notes share one
  CsoundUnity component.
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
