<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; A runaway: 50 times full scale, which is well past the default Loud Volume Warning threshold of
; 10. With the guard working this comes out as silence and one warning every couple of seconds; the
; recording is the proof, since a sample that was let through would read 50.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  a0 poscil 50, 220
  outs a0, a0
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
