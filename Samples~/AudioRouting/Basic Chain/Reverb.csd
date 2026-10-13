<Cabbage>
form caption("Reverb"), size(300, 100)

hslider bounds(0,  0, 300, 33) range(0, 0.99, 0.8,  1, 0.001) valuetextbox(1) channel("size") value(0.8)
hslider bounds(0, 33, 300, 33) range(0,    1,  0.7, 1, 0.001) valuetextbox(1) channel("mix")  value(0.7)
hslider bounds(0, 66, 300, 33) range(0,    1,  0.7, 1, 0.001) valuetextbox(1) channel("amp")  value(0.7)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Stage 3 of 3 — the end of the chain, and the only stage you need to hear.
;
; Reads the audio routed in from stage 2 and reverberates it. Mute the first two
; stages from the panel and this one still receives their signal: an AudioSource
; mute silences the monitor, not the route.

ksmps    = 32
nchnls   = 2
nchnls_i = 2
0dbfs    = 1

instr 1

  kSize chnget "size"
  kMix  chnget "mix"
  kAmp  chnget "amp"

  kSize portk kSize, 0.05
  kMix  portk kMix,  0.05
  kAmp  portk kAmp,  0.02

  aInL inch 1
  aInR inch 2

  ; reverbsc wants a feedback level below 1 and a cutoff in Hz.
  aWetL, aWetR reverbsc aInL, aInR, kSize, 12000

  aMixL ntrpol aInL, aWetL, kMix
  aMixR ntrpol aInR, aWetR, kMix

  outs aMixL * kAmp, aMixR * kAmp
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
