<Cabbage>
form caption("FM Voice"), size(300, 133)

hslider bounds(0,  0, 300, 33) range(20, 2000, 330, .5, 0.001) valuetextbox(1) channel("carrier")   value(330)
hslider bounds(0, 33, 300, 33) range(0,  1200, 600,  1, 0.001) valuetextbox(1) channel("deviation") value(600)
hslider bounds(0, 66, 300, 33) range(0,     1, 0.25, 1, 0.001) valuetextbox(1) channel("amp")       value(0.25)
hslider bounds(0, 99, 300, 33) range(0,     4, 0.7,  1, 0.001) valuetextbox(1) channel("rate")      value(0.7)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Stage 2 of 3 — reads the audio routed in from stage 1 and uses it as a modulator.
;
; The route writes into Csound's spin buffer, which is what inch reads. Nothing else
; is needed: Process Clip Audio concerns the AudioSource's clip, not routes.
; nchnls_i = 2 because two channels arrive.
;
; Its own result goes to fm_L and fm_R for the next stage, and to outs for monitoring. It plays
; repeated notes rather than a drone, so the reverb at the end of the chain has gaps to ring into.

ksmps    = 32
nchnls   = 2
nchnls_i = 2
0dbfs    = 1

instr 1

  kCarrier   chnget "carrier"
  kDeviation chnget "deviation"
  kAmp       chnget "amp"

  kCarrier   portk kCarrier,   0.02
  kDeviation portk kDeviation, 0.02
  kAmp       portk kAmp,       0.02

  ; The two channels routed in from the Oscillators instance.
  aInL inch 1
  aInR inch 2

  ; Frequency modulation: the incoming audio swings the carrier. At deviation 300 a
  ; full-scale input moves the carrier by ±300 Hz, so the beating of stage 1 is heard
  ; here as a moving timbre rather than as a pitch. The swing scales with the incoming
  ; amplitude, so it follows the level of stage 1.
  aOutL poscil kAmp, kCarrier + aInL * kDeviation
  aOutR poscil kAmp, kCarrier + aInR * kDeviation

  ; A pulse train rather than a held tone. The gap after each note is the point: that is where
  ; the reverb downstream is heard decaying on its own, which a continuous sound hides completely.
  ; rate = notes per second, 0 holds the note.
  kRate chnget "rate"
  kR    = (kRate > 0 ? kRate : 1)
  kPh   phasor kR
  kDuty = 0.5
  ; Written as a fraction of the period times the rate, which comes out as a fixed time: 25 ms of
  ; attack and 250 ms of release whatever the rate is. The release is capped so it always fits
  ; inside the note.
  kAtk  = 0.025 * kR
  kRel  = min(0.25 * kR, kDuty * 0.9)
  ; Smoothstep at both ends. A linear ramp has a corner in its slope where it meets zero, and the
  ; ear hears that corner as a tick however short the ramp is; this one leaves at zero slope and
  ; arrives at zero slope.
  kUp   = min(1, kPh / kAtk)
  kUp   = kUp * kUp * (3 - 2 * kUp)
  kDn   = min(1, max(0, (kDuty - kPh) / kRel))
  kDn   = kDn * kDn * (3 - 2 * kDn)
  kGate = (kPh >= kDuty ? 0 : min(kUp, kDn))
  kEnv  = (kRate <= 0 ? 1 : kGate)

  ; Interpolated up to audio rate before it multiplies audio: a k-rate envelope moves in steps of
  ; one control period, and those steps are heard as a faint buzz along the attack and the release.
  aEnv  interp kEnv

  aOutL = aOutL * aEnv
  aOutR = aOutR * aEnv

  chnset aOutL, "fm_L"
  chnset aOutR, "fm_R"

  outs aOutL, aOutR
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
