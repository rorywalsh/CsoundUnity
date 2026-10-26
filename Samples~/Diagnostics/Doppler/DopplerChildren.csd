<CsoundSynthesizer>
<Cabbage>
form caption("Doppler for children"), size(300, 110)

; The same three numbers as the single voice version, applied to both delay lines at once.
hslider bounds(0, 0, 300, 33) range(0.002, 0.12, 0.02, 1, 0.001) valuetextbox(1) channel("smoothing") value(0.02) text("Smoothing, seconds per pole")
hslider bounds(0, 36, 300, 33) range(0, 2, 2, 1, 1) valuetextbox(1) channel("poles") value(2) text("Poles, 0 is none")
combobox bounds(0, 72, 300, 33) channel("interp") items("8 points", "16 points", "32 points", "64 points") value(3) text("Tap reads")
</Cabbage>
<CsOptions>
</CsOptions>
<CsInstruments>

; The Doppler for CsoundUnityChild: one delay line per child, here, in the parent's orchestra.
;
; A child reads an audio channel this instance publishes and puts it somewhere in the scene. The
; signal is one and the same for all of them, so a delay applied once would be right for one child
; and wrong for the others: each is at its own distance and wants its own shift. So each gets a line
; of its own, fed by its own CsoundUnityDoppler, and reads the channel named after it.
;
; This instance makes no sound of its own: the children are its output.

ksmps  = 32
nchnls = 2
0dbfs  = 1

; One voice: a delay line whose tap follows a travel time. Written once and used per child, which is
; the whole point of doing it here rather than in each child.
opcode DopplerVoice, a, akkkk
  aSig, kDelay, kHalf, kPoles, kInterp xin

  kH      limit  kHalf, 0.001, 1
  kS1     portk  kDelay, kH
  kS2     portk  kS1, kH
  kSmooth =      (kPoles < 0.5) ? kDelay : ((kPoles < 1.5) ? kS1 : kS2)

  kDel    limit  kSmooth, 0.001, 0.99
  aDel    interp kDel

  aOut    init   0

  aDum    delayr  1
  ; The channel carries the combobox's position, 1 to 4, not the number of points: that is how a
  ; Cabbage combobox arrives here. Fixed sizes because deltapx takes its window at init time.
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

          xout    aOut
endop

instr 1
  aSig    poscil 0.3, 330

  kHalf   chnget "smoothing"
  kPoles  chnget "poles"
  kInterp chnget "interp"

  ; One CsoundUnityDoppler sits on each child and writes its own travel time here. The names come
  ; from the children themselves: a child reading "voice1" writes "voice1delay", so nothing has to be
  ; invented or kept in step by hand.
  kDelay1 chnget "voice1delay"
  kDelay2 chnget "voice2delay"

  aVoice1 DopplerVoice aSig, kDelay1, kHalf, kPoles, kInterp
  aVoice2 DopplerVoice aSig, kDelay2, kHalf, kPoles, kInterp

  ; Each child reads the channel named after it.
  chnset  aVoice1, "voice1"
  chnset  aVoice2, "voice2"

  ; Nothing is written to the output on purpose: this instance is heard through its children. Csound
  ; clears the output buffer every control cycle, so leaving it alone is silence. Writing 'outs 0, 0'
  ; is not: outs wants a-rate arguments, a bare 0 is not one, and the whole orchestra fails to
  ; compile over it, which looks exactly like a parent that is not running.
endin

</CsInstruments>
<CsScore>
f0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>
