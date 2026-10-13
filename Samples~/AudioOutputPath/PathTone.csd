<Cabbage>
form caption("Path Tone"), size(300, 67)

hslider bounds(0,  0, 300, 33) range(55, 880, 220, .5, 0.001) valuetextbox(1) channel("freq") value(220)
hslider bounds(0, 33, 300, 33) range(0,    1, 0.12, 1, 0.001) valuetextbox(1) channel("amp")  value(0.12)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; One tone, used by every instance in this sample: what changes between them is not the
; csd but the audio path the component is set to. A csd never knows which path carries it.
;
; sr comes from Unity's audio settings; ksmps below is used as written.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1

  kFreq chnget "freq"
  kAmp  chnget "amp"
  kFreq portk kFreq, 0.02
  kAmp  portk kAmp,  0.05

  ; A slow tremolo, so a stage that is frozen rather than silenced is recognisable:
  ; on return a muted instance has moved on, a paused one carries on where it stopped.
  kTrem oscili 0.25, 1.5
  aSig  poscil kAmp * (0.75 + kTrem), kFreq

  outs aSig, aSig
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
