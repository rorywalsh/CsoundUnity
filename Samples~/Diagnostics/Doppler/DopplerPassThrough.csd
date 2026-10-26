<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; The clip, straight back out. This instance takes its AudioSource's clip as input, with Process
; Audio Clip ON, so what Csound reads here is what Unity decided to hand the filter callback: the
; clip's own 330 Hz, resampled by whatever Unity does to the playback rate. That is the one way a
; Doppler shift can reach Csound at all, and this instrument does nothing that would hide it.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  aL, aR ins
  outs aL, aR
endin

</CsInstruments>
<CsScore>
f0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>
