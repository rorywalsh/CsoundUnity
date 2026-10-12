## Coming from Csound ##

This page is for those approaching Unity from Csound: composers and sound designers who are at home
in an orchestra and a score, and are meeting a game engine for the first time. It describes what
Unity is, what it replaces, and where the two worlds meet.

It assumes no Unity knowledge, and it does not teach Csound.

---

### What you are used to ###

In Cabbage or CsoundQt, one application gives you two things at once: an **editor** for the `.csd`,
and a **host** that runs it and puts a window on screen with the widgets you declared. You write the
interface and the audio in the same file, you press a button, and both appear.

Unity gives you the same two things, separately and on a different scale. It is the host, and it is
also where the interface lives — but the interface is a game, and the person using it is a player
rather than you at a mixing desk.

---

### What Unity is ###

A **scene** is what is running: a world with objects in it. Each object is a **GameObject**, which
on its own does nothing, and gets its behaviour from **Components** attached to it — a renderer, a
physics body, a script.

**CsoundUnity is one of those components.** It holds a Csound instance, compiles the `.csd` you give
it, and hands the audio to an **AudioSource**, Unity's own object for anything that makes sound.

The consequence worth internalising: there is no longer a moment when the piece ends. The game asks
for audio for as long as the scene is open, and the orchestra has to answer every time.

---

### Where the two worlds meet ###

**The `<Cabbage>` section still works, and it is the bridge.** CsoundUnity reads it exactly as a host
would: every widget you declare becomes a control channel, listed in the inspector with the range,
default and increment you wrote.

```
<Cabbage>
rslider  channel("gain"), range(0, 1, 0.4, 1, 0.01), text("Gain")
combobox channel("wave"), text("Sine", "Saw", "Square")
</Cabbage>
```

What changes is who moves them. In Cabbage you move them with the mouse. In Unity you can still do
that — the inspector gives you a slider while the game runs, which is how you tune by ear — but the
point is that **the game moves them**: a car's speed writes to `rpm`, a room's size writes to a
reverb's decay, a player's health writes to a filter cutoff.

The same widgets can also become on-screen controls for the player, without writing code: see
[UI Components](ui_components.md).

**Channels are unchanged.** `chnget` and `chnset` are what they always were. From the game's side
they are read and written with `SetChannel` and `GetChannel`, and notes are started with
`SendScoreEvent` — see [Controlling Csound from Unity scripts](controlling_csound_from_unity.md).

---

### What you have to change in the csd ###

Only two things are really about CsoundUnity rather than about Csound:

**Let Unity own the audio device.** Csound must not open one of its own; Unity collects the output
and routes it. In practice this means `-n` in `<CsOptions>` rather than `-odac`.

**Do not let the score end.** A score that finishes stops the performance, and in a game that reads
as the sound disappearing a few seconds in and never returning. End it with `f0 z`, and start
long-running instruments with `i"Name" 0 z`.

Sample rate is handled for you: CsoundUnity sets `sr` from Unity's output rate so the two always
agree, and the inspector can override it. `ksmps` stays yours.

Everything else in the `.csd` — opcodes, options, tables, the score syntax — is Csound, and the
[Csound manual](https://csound.com/docs/manual/index.html) remains the reference for it.

---

### What Unity gives you that a host does not ###

* **More than one orchestra at a time.** A host runs the `.csd` you opened. A scene can hold as many
  CsoundUnity components as you want, on different objects, each compiling its own `.csd` as a
  separate Csound instance: an engine on the car, a wind model on the terrain, a reverb on the room.
  They are independent — their own channels, their own score, their own output — and each one is a
  real Csound instance, so the cost is real too.
* **Instances can feed each other.** A named audio channel produced by one instance can be routed
  into the input of another, so an orchestra can process what a different orchestra produced, with
  the routing drawn in the editor rather than written in the csd. See
  [Audio Input Routing](audio_input_routing.md).
* **Position.** An AudioSource sits somewhere in the world, so an instrument is heard from where its
  object is, with distance attenuation and doppler. [CsoundUnityChild](csoundunity_child.md) does the
  opposite split: it takes named audio channels out of **one** orchestra and gives each its own place
  in the scene, which is cheaper than one instance per sound when they share instruments.
* **A mixer.** Groups, sends, effects and snapshots, with Csound's output as an ordinary signal
  inside it.
* **A timeline.** Score events and channel automation arranged on a track and scrubbed like video.
  See [Timelines](timelines.md).
* **Platforms.** The same project builds for desktop, mobile, and the browser — each with its own
  limits, listed in [Supported Platforms](platforms.md).

---

### Where to go next ###

* [Quick Start](quick_start.md) — install, a minimal csd, and a channel the game writes
* [The essentials](getting_started.md) — namespaces, initialisation, what 4.0 adds
* [Audio Output Path](audio_output_path.md) — how the audio reaches Unity, and the three ways it can
* [Loading external files](loading_external_files.md) — where sound files have to live once the
  project leaves your machine
* [Samples](samples.md) — working scenes, several built on Csound examples you may recognise
