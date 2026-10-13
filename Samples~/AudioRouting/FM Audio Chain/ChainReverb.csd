<Cabbage>
form caption("Chain Reverb"), size(300, 165)

hslider bounds(0,   0, 300, 33) range(0, 0.99, 0.9, 1, 0.001) valuetextbox(1) channel("fblvl")  value(0.9)
hslider bounds(0,  33, 300, 33) range(0,    1,  0.6, 1, 0.001) valuetextbox(1) channel("fco")    value(0.6)
hslider bounds(0,  66, 300, 33) range(0,   20,    1, 1, 0.001) valuetextbox(1) channel("pitchm") value(1)
hslider bounds(0,  99, 300, 33) range(0,    1,    1, 1, 0.001) valuetextbox(1) channel("mix")    value(1)
hslider bounds(0, 132, 300, 33) range(0,    1,    1, 1, 0.001) valuetextbox(1) channel("amp")    value(1)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Last stage of the FM Audio Chain: reverberates whatever the FM stage routes into it.
;
; The channel names are the ones the scene's script expects — fblvl, fco, pitchm, mix, amp — so
; it is a drop-in for any reverb in this chain.
;
; sr comes from Unity's audio settings; ksmps below is used as written.

ksmps    = 32
nchnls   = 2
nchnls_i = 2
0dbfs    = 1

instr 1

  kfblvl  chnget "fblvl"    ; tail length: reverbsc's feedback, 0.9 is already long
  kfco    chnget "fco"      ; damping: 0 is dark, 1 is bright
  kmix    chnget "mix"
  kamp    chnget "amp"

  kfblvl portk min(kfblvl, 0.99), 0.05
  kmix   portk kmix,  0.05
  kamp   portk kamp,  0.02

  ; fco maps 0..1 onto 20 Hz .. 20 kHz on an exponential curve, so the knob spends its travel
  ; where the ear is. The filter sits inside the feedback loop, which makes this a decay control
  ; as much as a tone one: at 0.5 the tail is gone in a moment, at 0.9 it rings on.
  kcurve  = (exp(4 * kfco) - 1) / (exp(4) - 1)
  kcutoff = 20 + kcurve * 19980

  aInL inch 1
  aInR inch 2

  ; Keeps denormals out of the feedback loop, which is where they cost CPU.
  denorm aInL, aInR

  ; pitchm is read once, at init: reverbsc takes its pitch modulation at i-rate.
  ipitchm chnget "pitchm"
  aWetL, aWetR reverbsc aInL, aInR, kfblvl, kcutoff, sr, ipitchm

  aMixL ntrpol aInL, aWetL, kmix
  aMixR ntrpol aInR, aWetR, kmix

  outs aMixL * kamp, aMixR * kamp
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
