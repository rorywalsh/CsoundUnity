<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; One channel, for the mono case: whatever Unity feeds, every output channel can only carry this.

ksmps  = 32
nchnls = 1
0dbfs  = 1

instr 1
  a0 poscil 0.5, 220
  out a0
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
