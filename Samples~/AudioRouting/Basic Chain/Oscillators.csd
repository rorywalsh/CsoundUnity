<Cabbage>
form caption("Oscillators"), size(300, 133)

hslider bounds(0,  0, 300, 33) range(20, 2000, 220, .5, 0.001) valuetextbox(1) channel("freq")   value(220)
hslider bounds(0, 33, 300, 33) range(0,    20,   3,  1, 0.001) valuetextbox(1) channel("detune") value(3)
hslider bounds(0, 66, 300, 33) range(0,     1, 0.25, 1, 0.001) valuetextbox(1) channel("amp")    value(0.25)
hslider bounds(0, 99, 300, 33) range(0,     4, 0.35, 1, 0.001) valuetextbox(1) channel("rate")   value(0.35)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Stage 1 of 3 — the source of the chain.
;
; Writes its audio to two named audio channels, osc_L and osc_R, and also plays it
; on its own AudioSource. The two are independent: an Audio Input Route on the next
; stage reads the named channels, and muting this AudioSource does not touch them.
;
; sr comes from Unity's audio settings; ksmps below is used as written.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1

  kFreq   chnget "freq"
  kDetune chnget "detune"
  kAmp    chnget "amp"

  kFreq   portk kFreq,   0.02
  kDetune portk kDetune, 0.02
  kAmp    portk kAmp,    0.02

  ; Two oscillators a few Hz apart: the beating makes the stereo width obvious, and gives the FM
  ; stage downstream something that moves. This one is deliberately continuous: it is the modulator,
  ; and the FM note needs it present for its whole length. The repeated notes are made downstream,
  ; where they belong.
  aLeft  poscil kAmp, kFreq - kDetune * 0.5
  aRight poscil kAmp, kFreq + kDetune * 0.5

  ; Notes, not a drone, and the same notes go to the route and to the output. Half the rate of the
  ; FM stage, so the modulator reaches it every other note: one FM note comes out with a timbre,
  ; the next as a bare sine. That alternation is what a route delivering audio sounds like, and
  ; muting this stage turns every note downstream into the bare sine.
  ; rate = notes per second, 0 holds the note.
  kRate chnget "rate"
  kR    = (kRate > 0 ? kRate : 1)
  kPh   phasor kR
  ; 0.45 of a 2.9 s period: a 1.3 s note and a 1.6 s gap. Shorter gaps read as a pulse, longer
  ; ones stop reading as a cycle at all.
  kDuty = 0.45
  kAtk  = 0.025 * kR
  kRel  = min(0.25 * kR, kDuty * 0.9)
  kUp   = min(1, kPh / kAtk)
  kUp   = kUp * kUp * (3 - 2 * kUp)
  kDn   = min(1, max(0, (kDuty - kPh) / kRel))
  kDn   = kDn * kDn * (3 - 2 * kDn)
  kGate = (kPh >= kDuty ? 0 : min(kUp, kDn))
  kEnv  = (kRate <= 0 ? 1 : kGate)
  ; To audio rate before it multiplies audio: k-rate steps are heard as a buzz on the fades.
  aEnv  interp kEnv

  aOutL = aLeft  * aEnv
  aOutR = aRight * aEnv

  ; What the route carries, gated like the output. The two stages are separate Csound instances and
  ; nothing aligns their clocks, so a note downstream can begin modulated and end bare; the fades
  ; are long enough that this reads as the timbre moving rather than as a click.
  chnset aOutL, "osc_L"
  chnset aOutR, "osc_R"

  outs aOutL, aOutR
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
