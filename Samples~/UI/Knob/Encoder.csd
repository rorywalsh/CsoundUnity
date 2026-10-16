<Cabbage>
form caption("Encoder"), size(420, 220)

encoder bounds( 30, 30, 110, 110) channel("semitones") increment(1)   popupPrefix("semitones ")
encoder bounds(170, 30, 110, 110) channel("cents")     increment(5)   popupPrefix("cents ")
hslider bounds( 30, 160, 360, 40)  range(0, 1, 0.3, 1, 0.001) valuetextbox(1) text("Level") channel("amp") value(0.3)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>

; An encoder has no minimum and no maximum: it reports how far it has been turned, for as long as
; you keep turning. That suits a quantity measured in steps from where you are — a transposition —
; and not one with ends, which is what a slider is for. The slider below is here for the contrast.
;
; Because nothing stops the value, the limits belong to the instrument: two octaves either way, and
; a semitone of fine tuning. Past that the encoder keeps counting and the pitch stays put.

ksmps  = 32
nchnls = 2
0dbfs  = 1

instr 1

  kSemi  chnget "semitones"
  kCents chnget "cents"
  kAmp   chnget "amp"

  kSemi  limit kSemi,  -24, 24
  kCents limit kCents, -100, 100

  ; 220 Hz is the starting point; the two encoders move it in semitones and in cents.
  kFreq = 220 * 2 ^ ((kSemi + kCents / 100) / 12)
  kFreq portk kFreq, 0.01
  kAmp  portk kAmp,  0.02

  aSig poscil kAmp, kFreq           ; a plain tone: the pitch is what this sample is about
  outs aSig, aSig

endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
