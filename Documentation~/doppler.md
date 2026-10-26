## Doppler ##

Unity's `dopplerLevel` does nothing to CsoundUnity. It shifts a pitch by resampling an AudioClip, and
CsoundUnity writes its audio in the filter callback, where there is no clip to resample. Measured in
the **Doppler** scene of the [Diagnostics sample](samples.md): moving a sound past the listener at a
third of the speed of sound, a plain AudioSource swept from 213 Hz to 446 Hz exactly as the geometry
predicts, while a CsoundUnity and four CsoundUnityChild held 330.00 Hz, on `OnAudioFilterRead` and on
`IAudioGenerator` alike.

Everything else Unity does with position still arrives: distance attenuation, panning, mixer effects.
Only the pitch has to be made in the orchestra, and that is what `CsoundUnityDoppler` is for.

The **Doppler** sample is the short version: one sound passing by, the component on it, and presets
to change what the sound is while it runs. The **Doppler** scene of the
[Diagnostics sample](samples.md) is the long version, where all of this was measured against Unity's
own.

### CsoundUnityDoppler

Put it on the GameObject that carries the CsoundUnity (or anywhere above it: it looks up the
hierarchy) and it writes the geometry between that object and the AudioListener into channels, every
frame.

| Field | What it sends |
|---|---|
| **Channel Prefix** | Goes in front of every name below, so several sources can write their own: `voice1` makes `voice1delay`. On a child, left empty, the name of the audio channel that child reads is used |
| **Distance Channel** | Distance to the listener, in Unity units |
| **Delay Channel** | That distance as a travel time in seconds, which is what a delay line wants: the speed of sound is divided out here rather than written again in the csd |
| **Closing Channel** | Closing speed, units per second, positive while the gap shrinks |
| **Ratio Channel** | The number to multiply a frequency by. 1 is no shift |
| **Speed Of Sound** | Units per second, 340 by default, which is the figure Unity uses and assumes one unit is one metre |
| **Doppler Level** | Scales the closing speed and the ratio, like Unity's own. The distance and the delay are geometry, so a delay line driven by them keeps its shift whatever this says |
| **Smoothing** | Seconds of smoothing on the speed. A speed measured over one frame jitters with the frame rate, and that is audible as a wobble |

Leave a channel name empty and that number is not sent. The names do not have to be declared
anywhere: they go straight to `chnget`.

### The physical way: a delay line

Send the **distance** and let a delay line do the rest. The tap follows `distance / speed of sound`,
and a tap that moves is a Doppler shift: it is right however the two sides move, and the sound also
arrives late by its own travel time. Unity delays nothing by distance, with a clip or without one:
its panner works on gain and on pitch.

```csound
instr 1
  aSig    poscil 0.3, 330

  kDelay  chnget "delay"        ; seconds, the travel time already worked out
  kSm1    portk  kDelay, 0.02   ; two poles: the steps go, the slope stays continuous
  kSmooth portk  kSm1,   0.02

  kDel    limit  kSmooth, 0.001, 0.99
  aDel    interp kDel

  aDum    delayr  1             ; one second of line reaches 340 units
  aOut    deltapx aDel, 32      ; 32 point interpolation on the moving tap
          delayw  aSig

  outs aOut, aOut
endin
```

This works on any signal, synthesised or recorded, because it never needs to know the frequency.

**Why the distance is not used as it arrives.** A channel is written once per Unity frame, so the
distance comes in steps, and between two steps it does not move at all. A tap that stops and starts
is a pitch that stops and starts: `interp` smooths over one control period, which spreads a step of
milliseconds across half a millisecond and then holds. Measured in the Doppler scene, that version
swept the right total range but followed the geometry at only r = 0.58, with spikes past the expected
shift and nothing in between, and it was audible as a warble while the sound crossed the scene.

So the tap carries on by itself at the speed that came with the last value, and snaps to the next one
when it arrives. That is what the `closing` channel is for, and why the age is capped: a component
that stops writing, because its object was disabled, would otherwise send the tap racing off.

Ramping to each value over a frame instead (`lineto kDist, 0.04`) measures right too, r = 0.97 once
its own lag is counted, but a ramp does not know where it is going: if it is shorter than the gap
between two frames it arrives and waits, and if it is longer it never arrives. Either way the tap's
speed pulses once a frame, which is the warble. What the `portk` is for is the little that is left,
the correction when the value that arrives disagrees with the prediction. Smoothing that costs no
shape, since the prediction is already carrying the movement, so its half-time can be generous: at 30
ms a snap worth a quarter of a millisecond of delay comes out as a 0.8% wobble rather than a 36% one.

**Csound has a `doppler` opcode, and this does not use it.** It is a plugin opcode, and the package
never relies on plugins, so a sample that needed one would not run everywhere this does. It is worth
knowing about outside the package: it takes a source position and a microphone position in metres and
returns the shifted signal, and under the hood it is this same recipe, a delay line whose read point
follows the distance, with the position smoothed by a one pole filter at 6 Hz by default and a speed
of sound of 340.29. It carries the same caveat too: the distance must not change faster than about
three quarters of the speed of sound.

**Use `deltapx`, not `deltapi`, and give it plenty of points.** A moving tap reads between two
samples, and `deltapi` fills that gap with a straight line. Stretching a pure tone by 15% that line is
audible: the glide comes out thin, and it sounds synthetic next to the same movement done by Unity.
What `deltapx` leaves instead is a set of faint lines that appear only while the tap moves, 75 to 87
dB down at 8 points. Quiet, but once everything louder has been dealt with they are what is left to
hear, so this uses 32. The price is 32 multiplies a sample.

**Which of the two to use.** If the sound is a recording, the cheapest and best Doppler is Unity's
own: turn **Process Audio Clip** ON and let it resample the clip on the way in. Its resampler moves
its read pointer sample by sample and knows nothing about frames, so it has no steps to smooth over,
which is why that source is the one that sounds right in the Diagnostics scene. The delay line is for
what Csound synthesises, where no clip exists and there is nothing for Unity to resample.

### On a CsoundUnityChild

A child holds no clip either: it reads an audio channel its parent published and writes it into the
buffer in the same callback, so Unity has nothing to resample there and `dopplerLevel` is as idle as
on the parent. Measured in the same scene, four children held 330.00 Hz throughout.

The shift for a child is made in the parent's orchestra, and it has to be made **once per child**:
each one is somewhere else, so each has its own distance, and one shift for all of them would be the
shift of none of them. Put a `CsoundUnityDoppler` on each child and it writes to the instance that
child reads, under names taken from the child itself, so a child reading `voice1` writes
`voice1delay` and there is nothing to invent or keep in step.

The orchestra then runs one line per voice. Written as a user defined opcode it is one line per
child, which is the whole reason for doing this in the parent rather than in each child:

```csound
opcode DopplerVoice, a, akkkk
  aSig, kDelay, kHalf, kPoles, kInterp xin
  ; the same two poles, limit, interp and deltapx as above
  xout aOut
endop

instr 1
  aSig    poscil 0.3, 330
  kDelay1 chnget "voice1delay"
  kDelay2 chnget "voice2delay"

  aVoice1 DopplerVoice aSig, kDelay1, kHalf, kPoles, kInterp
  aVoice2 DopplerVoice aSig, kDelay2, kHalf, kPoles, kInterp

  chnset  aVoice1, "voice1"
  chnset  aVoice2, "voice2"
  outs    0, 0
endin
```

Each child reads the channel named after it, and this instance makes no sound of its own: the
children are its output. The cost is one delay line per child, 384 KB and a `deltapx` a sample each,
and the parent stays a single Csound instance. The **Doppler** scene of the
[Diagnostics sample](samples.md) has it running, in `DopplerChildren.csd`.

**How close this gets to Unity's own.** Measured against it on the same movement, recorded one at a
time and lined up by their levels: the two pitch curves run from -65 to +65 Hz against Unity's -66 to
+65, with **no lag between them**, and they agree to within a few percent wherever the shift is large
enough to measure. Played at the same time they beat at 2 to 8 Hz, which is that few percent of a
shift of sixty: ten to forty cents, audible only because the two are sounding together. On its own
nobody can tell the two apart.

Playing the two together is worth knowing as a technique: a steady tone means they agree, and the
rate of the beating is the error in hertz. The Doppler scene has it on a button.

**A circle shifts nothing.** At a constant radius the distance never changes, so there is no Doppler
to hear however fast the orbit goes: the shift comes from the part of the movement along the line
between the two, which is why a flyby glides and a circle does not. The scene prints the closing speed
while it moves, and it reads 0 all the way round.

### The short way: multiply a frequency

If the orchestra owns its frequency, take the ratio and multiply:

```csound
instr 1
  kRatio chnget "dopplerRatio"
  aSig   poscil 0.3, 330 * kRatio
  outs aSig, aSig
endin
```

One `chnget` and no delay line, but it only applies where the frequency is yours to change, and it
gives no propagation delay.

### The one case where Unity's Doppler does reach Csound

With **Process Audio Clip** ON the AudioSource's clip is not a carrier, it is the input: Unity
resamples it before the callback, so Csound receives a signal already shifted. Measured in the same
scene, a csd handing its input straight back out came out at 213 to 446 Hz, like the plain AudioSource.
So a clip you pass through Csound keeps Unity's Doppler; what Csound synthesises never gets it.

`RootOutput` is outside all of this: it writes past the AudioSource and has no spatialisation at all,
not panning, not distance. See [RootOutput](root_output.md).
