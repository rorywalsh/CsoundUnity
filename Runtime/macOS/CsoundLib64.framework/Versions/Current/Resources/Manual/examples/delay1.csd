<CsoundSynthesizer>
<CsOptions>
; For Non-realtime ouput leave only the line below:
-o delay.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

; Initialize the global variables.
sr = 44100
kr = 4410
ksmps = 10
nchnls = 2
0dbfs  = 1

instr 1
  ; Make white noise.
  a0    random -1, 1

  ; Simple Lowpass filter
  a1    delay1  a0
  aout  =       (a0+0.99*a1)/2

  ; output white and filtered
        outs    aout, a0
endin


</CsInstruments>
<CsScore>
; Play Instrument #1.
i 1 0.0 3

e


</CsScore>
</CsoundSynthesizer>
