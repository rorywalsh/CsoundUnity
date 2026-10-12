## Coming from Unity ##

This page is for Unity developers approaching Csound for the first time: people who are at home in a
scene and a C# script, and have never written an orchestra. It describes what Csound is, what it
replaces, and where the two worlds meet.

It assumes no Csound knowledge, and it does not teach Unity.

---

### What you are used to ###

Sound in Unity normally arrives finished. You import a `.wav`, put it on an AudioSource, and play
it. To make it vary you layer more files, randomise the pitch, or cross-fade between takes — the
material itself is fixed at import time, and every variation has to exist as a recording.

---

### What Csound is ###

Csound is an audio engine you program. Instead of playing a recording back, you describe an
instrument — an oscillator, a filter, a physical model — and it **generates** the audio while the
game runs. There is no file to vary, because nothing was recorded: the sound is computed, and
anything the game knows can go into the computation.

The whole engine is one text file, the `.csd`, holding the instruments, the notes that start them,
and the options. CsoundUnity compiles it when the scene starts.

This is worth it when a sound has to follow something continuously — an engine across its whole rev
range, wind that tracks a storm, a creature whose size you can change at runtime — and it is usually
not worth it for a footstep or a UI click, where a file is simpler and cheaper.

---

### Where the two worlds meet ###

**CsoundUnity is a Component.** You add it to a GameObject, assign a `.csd`, and it adds an
AudioSource if there isn't one. From there the audio is an ordinary Unity signal: the mixer, effects,
3D positioning and distance attenuation all apply, exactly as they would for a clip.

Being a component, it behaves like one: a scene can hold as many as it needs, on different objects,
each compiling its own `.csd` independently — and one instance's output can be routed into another's
input, which is how an effect orchestra processes a source orchestra. Each instance is a full Csound
instance, so treat them as you would any other per-object cost.

**Channels are the API between the game and the sound.** A channel is a named value: the orchestra
reads it, your script writes it.

```csharp
using Csound.Unity;
using UnityEngine;

public class EngineSound : MonoBehaviour
{
    private CsoundUnity _csound;
    private Rigidbody   _body;

    void Start()
    {
        _csound = GetComponent<CsoundUnity>();
        _body   = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!_csound.IsInitialized) return;
        _csound.SetChannel("rpm", _body.velocity.magnitude * 300.0);
    }
}
```

The `IsInitialized` check is not optional: Csound compiles asynchronously, and calls made before it
is ready are dropped. [The essentials](getting_started.md#waiting-for-initialisation) covers the
patterns for this, and [Controlling Csound from Unity scripts](controlling_csound_from_unity.md) the
rest of the API.

**Channels can appear in the inspector.** Csound has a convention, borrowed from the Cabbage host,
for declaring the controls an instrument exposes — their range, default and step. CsoundUnity reads
that declaration and builds the inspector controls from it, so you can tune a sound by ear while the
game runs, and wire the same channels to on-screen controls without writing code. See
[UI Components](ui_components.md).

---

### Reading a `.csd` ###

You will have to read one before you write one. The file has three parts:

```
<CsOptions>      how Csound is run
<CsInstruments>  the instruments — this is where the sound is made
<CsScore>        which instruments start, when, and for how long
```

Two details are specific to running inside Unity rather than standalone: the options contain `-n`,
because Unity owns the audio device, and the score ends with `f0 z` so the performance does not stop
while the scene is still open. Everything else is Csound, and the
[Csound manual](https://csound.com/docs/manual/index.html) is the reference for it.

The fastest way in is a working scene. [Samples](samples.md) ships a number of them, from a single
oscillator to granular synthesis and physical models, each with the `.csd` next to the scene that
uses it.

---

### Where to go next ###

* [How to import CsoundUnity](importing.md), then [The essentials](getting_started.md)
* [Samples](samples.md) — start with the Basic category and read the `.csd` alongside the scene
* [Controlling Csound from the Unity Editor](controlling_csound_from_the_unity_editor.md) — what the
  inspector exposes, and how to audition a sound without entering Play mode
* [UI Components](ui_components.md) — sliders, buttons and keyboards bound to channels
* [Audio Output Path](audio_output_path.md) — the three ways Csound's audio can reach Unity, and
  what each costs in latency
