<Cabbage>
form caption("StereoOscChannels"), size(300, 330)

hslider bounds(0,  0, 300, 33) range(20, 16000, 440, .5, 0.001) valuetextbox(1) channel("freq")   value(440)
hslider bounds(0, 33, 300, 33) range(0,  30,      0,  1, 0.001) valuetextbox(1) channel("detune") value(0)
hslider bounds(0, 66, 300, 33) range(0,   1,    0.6,  1, 0.001) valuetextbox(1) channel("amp")    value(0.6)
hslider bounds(0,  99, 300, 33) range(0,   1,    0.0,  1, 0.001) valuetextbox(1) channel("output")   value(0.0)
hslider bounds(0, 132, 300, 33) range(0,   4,    0.0,  1, 0.001) valuetextbox(1) channel("rampDepth") value(0.0)
hslider bounds(0, 165, 300, 33) range(20, 4000, 660,  .5, 0.001) valuetextbox(1) channel("toneFreq")  value(660)
hslider bounds(0, 198, 300, 33) range(0,   10,  1.5,  1, 0.001) valuetextbox(1) channel("toneRate")   value(1.5)
hslider bounds(0, 231, 300, 33) range(0,    1,  0.7,  1, 0.001) valuetextbox(1) channel("toneAmp")    value(0.7)
hslider bounds(0, 264, 300, 33) range(0,    1, 0.75,  1, 0.001) valuetextbox(1) channel("toneDuty")   value(0.75)
hslider bounds(0, 297, 300, 33) range(0,    2, 0.25,  1, 0.001) valuetextbox(1) channel("toneRelease") value(0.25)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

; Stereo oscillator that writes its output to two named audio channels
; instead of the AudioSource output buffer.
;
; CsoundUnity setup:
;   - "audioL" and "audioR" are listed as Named Audio Channels in the inspector.
;     They appear in namedAudioChannelDataDict and can be routed to another
;     CsoundUnity instance via Audio Input Routes.
;   - "outs" is gated by the "output" channel, which defaults to 0: this instance
;     feeds the chain through its named channels while its own AudioSource stays
;     silent. Raise "output" to monitor it directly as well.
;
; Exposed channels (set from CsoundUnity inspector or scripts):
;   "freq"    - oscillator centre frequency in Hz  (default 440)
;   "detune"  - stereo detune spread in Hz         (default 0)
;   "amp"     - amplitude 0..1                     (default 0.6)
;   "output"  - own AudioSource output gate 0..1, does not affect
;               "audioL"/"audioR"                  (default 0)

sr     = 48000
ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1

  ; ---- parameters -------------------------------------------------------
  kFreq   chnget "freq"
  kDetune chnget "detune"
  kAmp    chnget "amp"
  kOutput chnget "output"
  
  ; Guard only kFreq against zero/negative (poscil undefined there).
  ; kDetune = 0 is valid (no stereo spread).
  ; kAmp    = 0 is valid (silence).
  kFreq = (kFreq < 1 ? 440 : kFreq)

  ; Smooth parameter changes
  kFreq   portk kFreq,   0.02
  kDetune portk kDetune, 0.02
  kAmp    portk kAmp,    0.02

  ; ---- Csound-side ramp, for telling "muted" from "paused" apart ------------
  ; A slow rising ramp that restarts every 8 seconds, multiplying the frequency
  ; by 1..(1 + rampDepth). Driven by phasor, so it advances on Csound's own
  ; k-cycles rather than from the host: it therefore keeps running while this
  ; instance is muted (Csound still performs) and freezes while it is paused
  ; (PerformKsmps is skipped). Coming back from a mute the ramp has moved on;
  ; coming back from a pause it resumes at exactly the point it stopped, which
  ; is the audible difference between the two.
  ; Silent by default (rampDepth = 0) so it never interferes with other tests.
  kRampDepth chnget "rampDepth"
  kRamp      phasor 0.125
  kFreq = kFreq * (1 + kRamp * kRampDepth)

  ; ---- startup fade-in ---------------------------------------------------
  ; Ramps amplitude from 0 to 1 over 0.3 s so the named channels
  ; always carry smoothly rising audio regardless of Unity init timing.
  aFadeIn linseg 0, 0.3, 1

  ; ---- stereo oscillator with detuned L / R for width -------------------
  aLeft  poscil kAmp * aFadeIn, kFreq - kDetune * 0.5
  aRight poscil kAmp * aFadeIn, kFreq + kDetune * 0.5

  ; ---- separate voice on its own channel, for CsoundUnityChild -----------
  ; audioL/audioR are the modulator: at 25 Hz they are barely audible on their
  ; own, and anything reading them sounds exactly like whatever they are
  ; modulating — so a child fed from them is impossible to pick out of the mix.
  ; This is an independent voice with its own pitch and its own pulse rate, so
  ; it sits clearly apart from the FM stage both in register and in rhythm.
  ; toneRate = 0 holds the note instead of repeating it.
  kToneFreq chnget "toneFreq"
  kToneRate chnget "toneRate"
  kToneAmp  chnget "toneAmp"
  kToneFreq = (kToneFreq < 1 ? 660 : kToneFreq)

  ; Same flat-topped gate as the FM stage: a held note with very short fades, so the
  ; timbre can be judged during it and the gap after it is unmistakable.
  ;   toneRate = notes per second, 0 holds the note (0.5 = one every two seconds)
  ;   toneDuty = fraction of that period the note sounds (0.75 of 2 s = a 1.5 s note)
  kToneDuty chnget "toneDuty"
  kTRate   = (kToneRate > 0 ? kToneRate : 1)
  kTonePh  phasor kTRate
  ; Fast attack, long eased release — see the note in FMInputModulator.csd: a linear
  ; ramp has a corner in its slope at zero, and the ear hears that corner as an
  ; abrupt stop. smoothstep has zero slope at both ends.
  kToneRelease chnget "toneRelease"
  kTAtk    = max(0.0001, 0.01 * kTRate)
  kTRel    = max(0.0001, min(kToneRelease * kTRate, kToneDuty * 0.9))
  kTUp     = min(1, kTonePh / kTAtk)
  kTDn     = min(1, max(0, (kToneDuty - kTonePh) / kTRel))
  kTDn     = kTDn * kTDn * (3 - 2 * kTDn)
  kTGate   = (kTonePh >= kToneDuty ? 0 : min(kTUp, kTDn))
  kToneEnv = (kToneRate <= 0 ? 1 : kTGate)
  aTone poscil kToneAmp * kToneEnv * aFadeIn, kToneFreq
  chnset aTone, "tone"

  ; ---- write to named audio channels ------------------------------------
  chnset aLeft,  "audioL"
  chnset aRight, "audioR"

  ; Gated by "output" (default 0): the named channels above always carry the
  ; signal, this only decides whether it is also monitored on the AudioSource.
  outs aLeft*kOutput, aRight*kOutput
endin

</CsInstruments>
<CsScore>
; Run indefinitely
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
