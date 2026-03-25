# CsoundUnity Timelines

CsoundUnity Timelines integrates Csound deeply with Unity's Timeline editor. You can trigger score events — single notes, procedural granular swarms, melodic arpeggios, euclidean rhythms, stochastic patterns, and chords, all sample-accurately scheduled inside Csound — and automate any named Csound channel over time to control parameters like filter cutoff, reverb amount, or volume directly from the Timeline.

---

## Requirements

- Unity 2021.3+
- Unity's **Timeline** package (`com.unity.timeline` 1.0.0+) — once installed, the CsoundUnity asmdef detects it automatically and enables the Timelines integration, no manual setup required
- A CsoundUnity component in the scene with a loaded `.csd` file

---

## Track Types

CsoundUnity adds two distinct track types to the Timeline editor:

| Track | Purpose |
|---|---|
| **Score Track** | Triggers Csound score events (notes, grains, arpeggios, patterns) at precise times |
| **Channel Track** | Animates a named Csound channel value over time (parameters, modulation) |

Use a **Score Track** when you want to schedule musical events. Events are sent as Csound score messages and scheduled sample-accurately inside Csound's audio thread.

Use a **Channel Track** when you want to automate a synthesis parameter that your `.csd` reads via `chnget`. Values are updated every Unity frame.

You can combine both track types freely on the same Timeline and bind them to the same CsoundUnity instance.

---

## Setup

### 1. Create a Timeline

1. Open the Timeline window (**Window → Sequencing → Timeline**)
2. Create a new Timeline asset and assign it to a `PlayableDirector` in the scene
3. If your Timeline contains **Score clips** that trigger notes from the very beginning, disable **"Play On Awake"** on the `PlayableDirector` and use `CsoundTimelineStarter` instead (see below)

### 2. Add Tracks

In the Timeline window, click **+** to add a track:

- **CsoundUnity Score Track** — for triggering score events
- **CsoundUnity Channel Track** — for animating Csound channels

For each track, drag your **CsoundUnity** component from the scene into the binding slot on the left.

### 3. Add Clips

Right-click on any track lane and select the corresponding **Add Clip** option. Each clip is configured independently in the Inspector.

### 4. CsoundTimelineStarter (Score clips only)

If your Timeline starts with Score clips that fire notes immediately, add the `CsoundTimelineStarter` component to any GameObject in the scene. It waits for Csound to finish initializing before starting the Timeline, ensuring the first note is never lost.

> This is only needed when Score clips are active from the very start of the Timeline. If Score clips start later (after Csound has had time to initialize), you can leave "Play On Awake" enabled.

| Field | Description |
|---|---|
| Director | The PlayableDirector to start (auto-found if empty) |
| Csound | The CsoundUnity instance to wait for (auto-found if empty) |
| Extra Delay | Additional seconds to wait after Csound is ready (default: 0.1s) |

---

## Score Track

A Score Track sends Csound score events from Timeline clips. Each clip has a **mode** that determines what kind of events it generates.

### Animating parameters

Any numeric parameter on a Score clip can be animated over time using Unity's Clip Properties curve editor. Select a clip, then open the **Clip Properties** panel in the Timeline window — you will see a coloured dot next to each animatable field. Click a dot to open a curve editor for that parameter. The inspector shows coloured diamonds next to any parameter that currently has a curve.

---

### Clip Modes

#### Score

Sends one Csound score line when the clip starts. The clip duration is automatically used as `p3`.

| Field | Description |
|---|---|
| Score | A raw Csound score string, e.g. `i1 0 1 440` |

Useful for sustained pads, one-shot sounds, or any event that fires once.

---

#### Swarm

Repeatedly fires short grains for the duration of the clip, creating a granular texture.

| Field | Description |
|---|---|
| Instr N | Csound instrument number |
| Grain Duration | Duration of each grain in seconds (`p3`) |
| Duration Variation | Randomises grain duration per grain (0 = fixed, 1 = ±100% of Grain Duration) |
| Delay | Time between grain onsets in seconds |
| Delay Variation | Random variation on the delay (0 = regular, 1 = fully random) |
| Pitch Base (Hz) | Center frequency for grains (`p4`) |
| Pitch Spread (Hz) | Random spread around the center frequency |
| Lookahead | How far ahead (in seconds) grains are pre-scheduled into Csound's queue |

**Lookahead guidance:**
- Too low (e.g. `0.01s`) — grains may arrive late during a slow frame, causing audible gaps
- Too high (e.g. `0.5s`) — queued grains will continue sounding for that long after the clip stops
- Recommended range: `0.05` – `0.1s`

The `.csd` receives each grain as: `i{N} {onset} {duration} {frequency_hz}`

---

#### Arpeggio

Plays a repeating melodic pattern across a scale or chord, synced to a tempo.

**Timing**

| Field | Description |
|---|---|
| BPM | Tempo of the arpeggio |
| Division | Note grid: Whole, Half, Quarter, Eighth, Sixteenth, Thirty-second |
| Note Duration | How long each note sounds (seconds). Shorter than the interval = staccato |
| Lookahead | How far ahead (in seconds) each note is pre-scheduled |
| Scheduling | **Precise**: notes are scheduled one cycle at a time — sample-accurate timing, at most one stale cycle after scrubbing. **BPM-note**: BPM is re-read at every note — enables tempo animation (accelerando/rallentando) within a cycle, timing ±1 frame |

**Note Source**

Choose **Scale** or **Chord** as the pitch source.

*Scale mode:*

| Field | Description |
|---|---|
| Pitch Base (Hz) | Root frequency (e.g. 220 = A3) |
| Scale | Chromatic, Major, Minor, Pentatonic, Dorian, Phrygian, Lydian, Mixolydian, HarmonicMinor |
| Octaves | How many octaves to span (1–4) |
| Direction | Up, Down, UpDown (ping-pong), Random |

*Chord mode:*

| Field | Description |
|---|---|
| Pitch Base (Hz) | Root frequency |
| Chord | Unison, MinorThird, MajorThird, Fourth, Fifth, Major, Minor, Dim, Aug, Sus2, Sus4, Major7, Minor7, Dom7, Dim7, m7b5, mMaj7, Aug7, AugMaj7, Major9, Minor9, Dom9, Add9, Major11, Minor11, Dom11, Major13, Minor13, Dom13, Custom |
| Octaves | How many octaves to span |
| Direction | Up, Down, UpDown, Random |

For **Custom** chord, enter semitone intervals from the root using the **+** / **−** buttons.

**Snap to Bars / Snap to Pattern**

The Inspector shows buttons to snap the clip duration to musically meaningful values:

- **Snap to bars** — sets the clip to 1, 2, 4, 8, or 16 bars at the current BPM
- **Snap to pattern** — sets the clip to exactly 1×, 2×, 3×, or 4× the full pattern cycle duration

The Inspector also displays the note interval, bar duration, note count, and full cycle duration for reference.

**CSD requirements:** `i{N} {onset} {duration} {frequency_hz}`

---

#### Euclidean

Distributes a number of hits as evenly as possible across a fixed number of steps, using the Bjorklund algorithm. Produces rhythmic patterns commonly found in world music and electronic percussion.

| Field | Description |
|---|---|
| Instr N | Csound instrument number |
| BPM | Tempo |
| Division | Step grid: Whole, Half, Quarter, Eighth, Sixteenth, Thirty-second |
| Note Duration | Duration of each triggered note |
| Steps | Total number of steps in the pattern (1–32) |
| Hits | Number of active hits distributed across the steps (1–Steps) |
| Rotation | Rotates the pattern by N steps (shifts the downbeat) |
| Scheduling | Same as Arpeggio: Precise or BPM-step |

**CSD requirements:** `i{N} {onset} {duration} {frequency_hz}`

---

#### Pattern

A step sequencer with multiple independent lanes (instruments), each with its own toggle grid. Every lane fires its Csound instrument on each active step. Typical use: drum machines, rhythmic sound design.

**Timing**

| Field | Description |
|---|---|
| BPM | Tempo of the pattern |
| Division | Step grid: Whole, Half, Quarter, Eighth, Sixteenth, Thirty-second |
| Steps | Total number of steps per cycle (1–32) |
| Lookahead | How far ahead (in seconds) the cycle is pre-scheduled into Csound. Increase if you hear occasional late starts (see note below) |
| Scheduling | **Precise**: the entire cycle is sent to Csound at once — sample-accurate, immune to frame jitter. **BPM-step**: one step at a time, allows live BPM changes within a cycle, timing ±1 frame |

**Lanes**

Each lane represents one instrument and has:

| Field | Description |
|---|---|
| Label | Display name (for the Inspector grid) |
| Instr N | Csound instrument number to trigger on active steps |
| Enabled | Mutes the entire lane without removing it |
| Velocity Mode | **Fixed**: all steps use the same velocity. **Offbeat**: alternate steps use a lower velocity for a natural groove feel |
| Velocity | Base velocity (0–1), sent as `p4` |
| Accent Velocity | Velocity for downbeat / accented steps |
| Pan | Stereo position (−1 left, 0 centre, +1 right), sent as `p5` |
| Pattern | The toggle grid: one button per step, click to activate or deactivate |

**Snap to bars / Snap to pattern** buttons in the Inspector set the clip duration to musically aligned values.

**CSD requirements:** `i{N} {onset} {duration} {velocity} {pan}`

**Lookahead guidance (Precise mode)**

In Precise mode, the entire cycle is pre-scheduled `patternLookahead` seconds before it starts. If a Unity frame takes longer than the lookahead window, the cycle fires late:

- Default lookahead: `0.1 s` (100 ms) — robust for most hardware
- Increase to `0.2 s` or more if you hear occasional missed beats on slower machines
- Very high values (> cycle duration) are safe but unnecessary

> **Performance note:** Audio timing in Unity is tightly coupled to CPU performance. Always test builds with the power adapter connected. Without it, CPU throttling can introduce timing irregularities that make even correct code sound out of time.

**Changing the pattern from code**

Use the Pattern runtime API (see [Runtime API](#runtime-api) below) to toggle steps, mute lanes, and change tempo while the Timeline is playing.

---

#### Stochastic

Fires notes at a regular rhythmic grid, with each step having a probability of triggering. Pitch is chosen randomly from a scale or chord, optionally weighted toward the center.

| Field | Description |
|---|---|
| Instr N | Csound instrument number |
| BPM | Tempo |
| Division | Step grid |
| Note Duration | Duration of each note |
| Note Source | Scale or Chord |
| Pitch Base (Hz) | Root frequency |
| Scale / Chord | Pitch set (same options as Arpeggio) |
| Octaves | How many octaves to span |
| Hit Probability | Chance (0–1) that any given step triggers a note |
| Pitch Weight | 0 = uniform random pitch; 1 = strongly weighted toward the center pitch |

**CSD requirements:** `i{N} {onset} {duration} {frequency_hz}`

---

#### Chord

Fires a set of notes simultaneously (or as a strum) on a trigger.

| Field | Description |
|---|---|
| Instr N | Csound instrument number |
| Note Duration | Duration of each note |
| Note Source | Scale or Chord |
| Pitch Base (Hz) | Root frequency |
| Chord Type | Chord voicing |
| Octaves | How many octaves to voice the chord across |
| Strum Spread | Seconds between successive notes (0 = block chord, >0 = guitar-style strum) |
| Repeat | When enabled, re-triggers the chord at the specified Division throughout the clip |
| Division | Re-trigger interval (only when Repeat is enabled) |

**CSD requirements:** `i{N} {onset} {duration} {frequency_hz}`

---

### Clip Duration and Looping

For Arpeggio and Euclidean modes, the clip duration should be an exact multiple of the pattern cycle to avoid a partial last cycle. Use **Snap to Pattern** to set the duration automatically.

When a clip ends, any notes still pending in Csound's queue are cancelled immediately (`i -N 0 0`). Transitions between clips are clean with no overlap or double-triggering.

---

## Channel Track

A Channel Track animates the value of a named Csound channel over time, letting you control any `chnget`/`chnset` parameter directly from the Timeline.

### Setup

1. Click **+** in the Timeline window and select **CsoundUnity Channel Track**
2. Select the channel name from the dropdown (populated automatically from the CSD file) or type the name manually
3. Drag your **CsoundUnity** component into the track binding slot
4. Right-click the lane and select **Add CsoundUnity Channel Clip**

### Clip Modes

Each Channel clip has a **Mode** that determines how its value is generated:

| Mode | Behaviour |
|---|---|
| **Fixed** | Outputs a constant value for the duration of the clip |
| **Random (S&H)** | Jumps to a new random value at a set rate (sample and hold) |
| **Random Smooth** | Interpolates smoothly to a new random target at a set rate |

For Random modes, set **Min**, **Max**, and **Rate (Hz)**. The **Use CSD range** button populates Min and Max from the channel definition in the CSD file.

### How it works

The mixer blends values across overlapping clips using Unity's standard Timeline weight system:

- **No overlap** — the channel is set to the clip's value while the clip is active
- **Overlapping clips** — values are blended proportionally using each clip's ease weights
- **Gap between clips** — the channel holds its last written value (does not reset to zero)

### CSD side

```csound
kCutoff chnget "cutoff"
```

The channel is updated every Unity frame, so resolution is limited to the frame rate. For sample-accurate timing, use score events instead.

### Tips

- Use **ease in / ease out** on clips (right-click → Edit) to create smooth ramps
- Stack multiple Channel Tracks to control several parameters on the same CsoundUnity instance independently

---

## Troubleshooting

**First clip starts late or sounds out of time**
→ Disable **Play On Awake** on the `PlayableDirector` and add `CsoundTimelineStarter` to the scene.

**Gap of silence at the end of each cycle (Arpeggio / Euclidean)**
→ Use **Snap to Pattern** to align the clip duration to the exact pattern length. Check that `Note Duration` is not shorter than the note interval if you want legato.

**Notes keep sounding after scrubbing or stopping**
→ The instrument number must be a plain integer (e.g. `1`, not a named instrument) for `i -N 0 0` to work. If the instrument uses fractional numbers, they will not be cancelled.

**Channel value jumps instead of ramping**
→ Add ease in / ease out to the Channel clip (right-click → Edit clip). For longer ramps, overlap two clips with opposing ease curves.

**No channels appear in the Channel Track dropdown**
→ Make sure the CsoundUnity component has a CSD file assigned. The channel list is read from the CSD at import time.

**Pattern (or other modes) sounds irregular / out of time in a build**
→ If on a laptop, always test with the **power adapter connected**. Without it, macOS and Windows reduce CPU clock speed (throttling), introducing frame timing jitter that can make the patterns sound out of time. This applies especially to audio-intensive builds. If irregularities persist with the adapter connected, try increasing the `Lookahead` value on the clip (e.g. from `0.1` to `0.2` s).

---

## Runtime API

You can read and write parameters on a Score clip while the Timeline is playing. Each clip exposes a set of typed setters on `CsoundUnityScorePlayableBehaviour`.

### Accessing the behaviour at runtime

Iterate the playable graph to reach the behaviour for a specific clip:

```csharp
using Csound.Unity.Timelines;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ScoreController : MonoBehaviour
{
    public PlayableDirector director;

    CsoundUnityScorePlayableBehaviour _patternBehaviour;

    void Start()
    {
        _patternBehaviour = GetFirstPatternBehaviour();
    }

    CsoundUnityScorePlayableBehaviour GetFirstPatternBehaviour()
    {
        var timeline = director.playableAsset as TimelineAsset;
        var graph    = director.playableGraph;
        int outIdx   = 0;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is CsoundUnityScoreTrack)
            {
                var output = graph.GetOutput(outIdx);
                var mixer  = (ScriptPlayable<CsoundUnityScoreMixerBehaviour>)output.GetSourcePlayable();

                for (int i = 0; i < mixer.GetInputCount(); i++)
                {
                    var clipPlayable = (ScriptPlayable<CsoundUnityScorePlayableBehaviour>)mixer.GetInput(i);
                    var behaviour    = clipPlayable.GetBehaviour();
                    if (behaviour.scoreInfo.mode == ScoreMode.Pattern)
                        return behaviour;
                }
            }
            outIdx++;
        }
        return null;
    }

    void Update()
    {
        if (_patternBehaviour == null) return;

        // Change tempo live
        _patternBehaviour.SetPatternBpm(140f);

        // Toggle step 4 on lane 0 (kick drum)
        _patternBehaviour.SetPatternStep(0, 4, true);

        // Mute lane 2 (hi-hat)
        _patternBehaviour.SetPatternLaneEnabled(2, false);
    }
}
```

> Changes take effect on the **next cycle** — the current cycle's notes have already been sent to Csound.

---

### Arpeggio setters

| Method | Description |
|---|---|
| `SetArpBpm(float bpm)` | Change tempo |
| `SetArpDivision(RhythmicDivision d)` | Change note grid (e.g. `Eighth`, `Sixteenth`) |
| `SetArpPitchBase(float hz)` | Change root pitch |
| `SetArpNoteDuration(float s)` | Change note duration |
| `SetArpOctaves(int o)` | Change number of octaves (1–4) |
| `SetArpScale(Scale scale)` | Change scale (e.g. `Scale.Minor`) |
| `SetArpChord(Chord chord)` | Change chord voicing |
| `SetArpDirection(ArpDirection d)` | Change direction (`Up`, `Down`, `UpDown`, `Random`) |
| `SetArpNoteSource(ArpNoteSource s)` | Switch between `Scale` and `Chord` |
| `SetArpCustomIntervals(int[] i)` | Set custom semitone intervals for the Custom chord |

---

### Swarm setters

| Method | Description |
|---|---|
| `SetSwarmPitchBase(float hz)` | Change center frequency |
| `SetSwarmPitchSpread(float hz)` | Change random pitch spread |
| `SetSwarmDelay(float s)` | Change inter-grain delay |
| `SetSwarmDelayVariation(float v)` | Change delay randomness (0–1) |
| `SetSwarmGrainDuration(float s)` | Change grain duration |
| `SetSwarmNoteDurationVariation(float v)` | Change duration randomness (0–1) |

---

### Euclidean setters

| Method | Description |
|---|---|
| `SetEuclideanBpm(float bpm)` | Change tempo |
| `SetEuclideanDivision(RhythmicDivision d)` | Change step grid |
| `SetEuclideanHits(int hits)` | Change number of active hits |
| `SetEuclideanSteps(int steps)` | Change total step count (1–32) |
| `SetEuclideanRotation(int r)` | Rotate the pattern by N steps |
| `SetEuclideanPitch(float hz)` | Change pitch |
| `SetEuclideanNoteDuration(float s)` | Change note duration |

---

### Pattern setters

| Method | Description |
|---|---|
| `SetPatternBpm(float bpm)` | Change tempo |
| `SetPatternDivision(RhythmicDivision d)` | Change step grid |
| `SetPatternStep(int lane, int step, bool active)` | Toggle a single step on or off |
| `SetPatternLanePattern(int lane, bool[] pattern)` | Replace the entire step grid for a lane |
| `SetPatternLaneEnabled(int lane, bool enabled)` | Mute or unmute a lane |
| `SetPatternLaneVelocity(int lane, float v)` | Set base velocity (0–1) |
| `SetPatternLaneAccentVelocity(int lane, float v)` | Set accent velocity (0–1) |
| `SetPatternLanePan(int lane, float pan)` | Set pan (−1 left, 0 centre, +1 right) |

---

### Common setters

| Method | Description |
|---|---|
| `SetBpm(float bpm)` | Change tempo (works for any mode that uses BPM) |
| `SetPitchBase(float hz)` | Change root pitch |
| `SetNoteDuration(float s)` | Change note duration |
| `SetInstrN(string n)` | Change the Csound instrument number |
| `SetMode(ScoreMode m)` | Switch clip mode at runtime |
