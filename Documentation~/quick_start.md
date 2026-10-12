## Quick Start ##

From an empty project to a sound you can change from code, in one page. Nothing here needs another
page to make sense; links are for going deeper afterwards.

---

### 1. Install the package ###

**Window → Package Manager → + → Add package from git URL…**, paste this and press **Add**:

```
https://github.com/rorywalsh/CsoundUnity.git
```

Other ways to install, and what to do when upgrading from 3.x, are in
[How to import](importing.md).

---

### 2. Write the csd ###

A `.csd` is one text file holding the instruments and the notes that start them. Create
`FirstSound.csd` anywhere under `Assets` and paste this in:

```csound
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr Tone
    kFreq chnget "freq"
    if kFreq == 0 then
        kFreq = 440
    endif

    aSig  oscili 0.2, kFreq
          outs   aSig, aSig
endin

</CsInstruments>
<CsScore>
f0 z
i"Tone" 0 z
</CsScore>
</CsoundSynthesizer>
```

Two details are about running inside Unity rather than standalone:

- **`f0 z` and `i"Tone" 0 z`** keep the performance and the instrument alive indefinitely. A score
  that ends stops Csound, which in a game reads as the sound vanishing a few seconds in. This one
  you do have to write.
- **There is no `sr` line, and adding one would change nothing**: CsoundUnity sets the sample rate
  from Unity's own before compiling, so the two always agree. The **Override Sample Rate** toggle in
  the inspector is how you take that decision back. `ksmps` works the other way — the value in the
  csd is the one used.

`-n` and `-d` are set by CsoundUnity whether or not the csd asks for them, so `<CsOptions>` could be
left empty here. They are written out because that is what a csd normally carries.

---

### 3. Add the component ###

1. Select a GameObject in the scene — an empty one is fine.
2. **Add Component → CsoundUnity**. An `AudioSource` is added for you if there isn't one.
3. Drag `FirstSound.csd` into the **Csd Asset** field.

---

### 4. Press Play ###

You hear a 440 Hz sine wave.

**If you hear nothing:**

| Check | |
|---|---|
| The Console | CsoundUnity logs `done init, compiledOk? True/False`. **That line is all you get by default** |
| **Log Csound Output** in the inspector | turn it on to see *why* it failed — Csound's own messages, including the compile errors, are off until you do |
| **Csd Asset** is really assigned | an empty field means nothing to compile |
| The AudioSource is not muted, volume is not 0 | it is an ordinary AudioSource |
| There is an **AudioListener** in the scene | normally on the Main Camera |

---

### 5. Make it change ###

The instrument already reads a channel called `freq`. A channel is a named value: the orchestra
reads it, your game writes it.

Add a script to the same GameObject:

```csharp
using Csound.Unity;
using UnityEngine;

public class SweepPitch : MonoBehaviour
{
    private CsoundUnity _csound;

    void Start() => _csound = GetComponent<CsoundUnity>();

    void Update()
    {
        if (!_csound.IsInitialized) return;
        _csound.SetChannel("freq", 220.0 + Mathf.PingPong(Time.time * 100f, 440f));
    }
}
```

Press Play: the pitch sweeps.

The `IsInitialized` check is there because on WebGL Csound is still loading for the first moments,
and calls made before it is ready are dropped. On every other platform it is already true by the
time `Start` runs — see [the initialization contract](lifecycle.md#when-each-call-completes).

---

### 6. Put the channel in the inspector ###

Declaring the channel in a `<Cabbage>` section at the top of the csd gives you a slider in the
CsoundUnity inspector, usable while the game runs:

```
<Cabbage>
rslider channel("freq"), range(110, 880, 440, 1, 0.01), text("Frequency")
</Cabbage>
```

`range(min, max, default, skew, increment)`. The same declaration is what the
[UI components](ui_components.md) bind to, so the slider can become an on-screen control without
writing code.

---

### Where to go next ###

* [Coming from Unity](coming_from_unity.md) or [Coming from Csound](coming_from_csound.md), depending
  on which half is unfamiliar
* [The essentials](getting_started.md) — namespaces, initialisation, what 4.0 adds
* [Controlling Csound from Unity scripts](controlling_csound_from_unity.md) — channels, score events,
  starting and stopping instruments
* [Samples](samples.md) — working scenes, from one oscillator to granular synthesis
