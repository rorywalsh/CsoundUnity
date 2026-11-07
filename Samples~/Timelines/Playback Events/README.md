# Timeline playback events

Open `StepAndPatternTimeline_PlaybackEvents.unity` and enter Play mode. This independent copy of the
musical-transport example keeps its tempo automation and mixed clip lengths. The overlay shows
percussion flashes (orange), pitched notes (cyan), current clip names and the latest sequencer step.
Horizontal position follows pan; size follows velocity; pitched notes move vertically with frequency.
Pitch and rhythm have separate vertical bands and fixed-height labels. Pitch labels reuse MusicUtils
note conversion with cached names; rhythm labels use the existing lane names (BD, SD, etc.).
The visualizer reuses 48 pulse slots and subscribes/unsubscribes through the controller's C# API.
Its script uses the shared `CsoundUnity.TimelineSamples.asmdef` in the parent Timelines folder.

The controller's **Playback Events** foldout enables notifications and exposes UnityEvent callbacks.
Clip foldouts select note/step emission and carry `rhythm` or `melody` tags. Emission is off by default
on new clips; this sample explicitly enables both options. The visualizer accepts an
optional exact track-name or tag filter. Its public receivers also appear in UnityEvent menus; disable
**Subscribe In Code** before wiring them in the inspector. No timeline markers or signal tracks are required.

Compare events enabled/disabled at normal and high Tempo Scale, change BPM, cross clip boundaries,
loop, pause/resume, stop/restart and rebuild the graph during playback. Pause and rebuild should not
produce false clip exits. Pausing discards pending visual predictions; already queued Precise audio
is not canceled. Predicted onset callbacks have rendered-frame precision, not sample precision.

For other generators, attach the visualizer to their existing sample controller and enable Playback
Events. Stochastic should notify only successful hits; Euclidean step events include rests; Chord
notes should appear individually with strum. Swarm can be dense: disable note events per clip or keep
notifications disabled when unused. Free Score text emits clip lifecycle events only.
