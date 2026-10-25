<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Six channels, each on its own pitch and its own level, so one recording says where each one
; landed and which ones never left Csound at all.
;
;   channel 1   220 Hz   0.50      channel 4   392 Hz   0.35
;   channel 2   277 Hz   0.45      channel 5   440 Hz   0.30
;   channel 3   330 Hz   0.40      channel 6   523 Hz   0.25

ksmps  = 32
nchnls = 6
0dbfs  = 1

instr 1
  a0 poscil 0.50, 220
  a1 poscil 0.45, 277
  a2 poscil 0.40, 330
  a3 poscil 0.35, 392
  a4 poscil 0.30, 440
  a5 poscil 0.25, 523
  chnset a0, "audiochanneltest"
  outch 1, a0, 2, a1, 3, a2, 4, a3, 5, a4, 6, a5
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
