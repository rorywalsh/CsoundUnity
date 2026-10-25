<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Not a number: the square root of a negative signal. Nothing compares true against NaN, so the
; same comparison that catches a runaway catches this, whatever the threshold is set to. It matters
; most on RootOutput, where what an instance writes goes into Unity's own main output.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  aNeg  poscil 1, 220
  aNeg  =      -1 - abs(aNeg)
  a0    sqrt   aNeg
  outs a0, a0
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
