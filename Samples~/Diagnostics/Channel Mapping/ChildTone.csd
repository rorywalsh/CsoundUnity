<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; A parent for the CsoundUnityChild cases. It publishes two named audio channels and writes
; nothing to its own output, so whatever a recording picks up came from a child.
;
;   chanA   220 Hz   0.50
;   chanB   660 Hz   0.25

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  a0 poscil 0.50, 220
  a1 poscil 0.25, 660
  chnset a0, "chanA"
  chnset a1, "chanB"
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
