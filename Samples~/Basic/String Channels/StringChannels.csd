<Cabbage>
form caption("String Channels"), size(300, 34)

hslider bounds(0, 0, 300, 33) range(55, 880, 220, .5, 0.001) valuetextbox(1) channel("freq") value(220)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; Two channels that carry text instead of numbers.
;
;   "shape"  — read from Unity: which waveform to play, by name
;   "status" — written back for Unity: the name this instrument actually honoured
;
; A string channel is read with chnget into an S variable and compared with strcmpk, which works at
; k-rate and so follows the value while the scene runs. Numbers would have done this job too; what
; a string buys is that both sides name the same thing the same way, with no table of magic numbers
; to keep in step between the csd and the game.

ksmps  = 32
nchnls = 2
0dbfs  = 1

giSine ftgen 1, 0, 4096, 10, 1
giSaw  ftgen 2, 0, 4096, 10, 1, 1/2, 1/3, 1/4, 1/5, 1/6, 1/7, 1/8
giSqr  ftgen 3, 0, 4096, 10, 1, 0, 1/3, 0, 1/5, 0, 1/7, 0, 1/9

instr 1

  kFreq chnget "freq"
  kFreq portk kFreq, 0.02

  Sshape chnget "shape"

  ; Unknown names fall back to the sine rather than going silent: a sample that answers nothing
  ; when it is given something unexpected is hard to tell from one that is broken.
  kFn = 1
  if strcmpk(Sshape, "saw") == 0 then
    kFn = 2
  elseif strcmpk(Sshape, "square") == 0 then
    kFn = 3
  endif

  aSig oscilikt 0.2, kFreq, kFn
  outs aSig, aSig

  ; What Unity reads back. The round trip is the point: the game asks by name and is told, by name,
  ; what it got — which is how it finds out that a name was not understood.
  if kFn == 2 then
    Sstatus = "saw"
  elseif kFn == 3 then
    Sstatus = "square"
  else
    Sstatus = "sine"
  endif
  chnset Sstatus, "status"

endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
