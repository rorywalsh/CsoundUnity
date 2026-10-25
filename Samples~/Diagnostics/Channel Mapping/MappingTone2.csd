<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Two channels that cannot be mistaken for one another: different pitch, so the WAV says which
; Csound channel reached a speaker, and different amplitude, so the recorder's own peak and RMS
; figures say it too.
;
;   channel 1   220 Hz   0.50
;   channel 2   660 Hz   0.25

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  a0 poscil 0.50, 220
  a1 poscil 0.25, 660
  outs a0, a1
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
