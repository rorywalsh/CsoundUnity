<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; A tone that holds still, so anything heard while the listener moves came from the movement.
;
; Steady and pure on purpose: a click, a zipper or a jump in pitch stands out against it, and in a
; recording it stands out as a step between two samples far larger than a sine of this frequency
; can make. The same signal goes out of this instance and into a named channel, so the children
; reading that channel and this instance can be compared under the same movement.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  aTone poscil 0.3, 330
  chnset aTone, "tone"
  outs aTone, aTone
endin

</CsInstruments>
<CsScore>
f0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>
