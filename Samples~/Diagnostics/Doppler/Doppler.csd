<CsoundSynthesizer>
<Cabbage>
form caption("Doppler"), size(300, 110)

; The three numbers worth trying by ear. They show up in the inspector's Control Channels, so a
; setting can be changed while the scene plays without touching this file.
hslider bounds(0, 0, 300, 33) range(0.002, 0.12, 0.02, 1, 0.001) valuetextbox(1) channel("smoothing") value(0.02) text("Smoothing, seconds per pole")
hslider bounds(0, 36, 300, 33) range(0, 2, 2, 1, 1) valuetextbox(1) channel("poles") value(2) text("Poles, 0 is none")
combobox bounds(0, 72, 300, 33) channel("interp") items("8 points", "16 points", "32 points", "64 points") value(3) text("Tap reads")
</Cabbage>
<CsOptions>
</CsOptions>
<CsInstruments>

; The Doppler made in the orchestra.
;
; CsoundUnityDoppler writes how long the sound takes to reach the listener into the "delay" channel,
; and the tap of this delay line follows it. A tap that moves is a Doppler shift, it is right however the two sides
; move, and the sound also arrives late by its own travel time, which Unity's Doppler does not do.
;
; The component sends the delay already in seconds, so the speed of sound lives in one place, its
; Speed Of Sound field, instead of being written again here. One second of line reaches 340 units at
; that speed; past that the tap would wrap, so the limit holds it inside.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1
  aSig  poscil 0.3, 330

  ; The three numbers the sliders above drive, so a setting can be judged by ear from the inspector
  ; without touching this file. The defaults are what measured best; what each one costs is in
  ; Documentation~/doppler.md.
  kDelay  chnget "delay"            ; seconds of travel time, from CsoundUnityDoppler
  kHalf   chnget "smoothing"        ; half-time of each pole, in seconds
  kPoles  chnget "poles"            ; 0, 1 or 2
  kInterp chnget "interp"           ; the combobox above: 1 is 8 points, 4 is 64

  ; Both poles always run, and the one to use is picked after: a filter that is switched off and on
  ; again comes back holding whatever it had, and that jump would be heard.
  kH      limit  kHalf, 0.001, 1
  kS1     portk  kDelay, kH
  kS2     portk  kS1, kH
  kSmooth =      (kPoles < 0.5) ? kDelay : ((kPoles < 1.5) ? kS1 : kS2)

  kDel    limit  kSmooth, 0.001, 0.99
  aDel    interp kDel

  aOut    init   0

  aDum    delayr  1
  ; A ladder of fixed sizes, because deltapx takes its window at init time and cannot be slid while
  ; it plays, and restarting the instrument for each new size would empty the delay line, which is
  ; heard. The channel carries the combobox's position, 1 to 4, not the number of points: that is
  ; how a Cabbage combobox arrives here.
  if kInterp < 1.5 then
    aOut  deltapx aDel, 8
  elseif kInterp < 2.5 then
    aOut  deltapx aDel, 16
  elseif kInterp < 3.5 then
    aOut  deltapx aDel, 32
  else
    aOut  deltapx aDel, 64
  endif
          delayw  aSig

  outs aOut, aOut
endin

</CsInstruments>
<CsScore>
f0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>
