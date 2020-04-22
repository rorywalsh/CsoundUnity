<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in    No messages
-odac           -iadc     -d     ;;;RT audio I/O
; For Non-realtime ouput leave only the line below:
; -o 0dbfs.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

; Initialize the global variables.
sr = 44100
kr = 4410
ksmps = 10
nchnls = 1

; Set the 0dbfs to 1.
0dbfs = 1

; Instrument #1.
instr 1
  ; Linearly increase the amplitude value "kamp" from 
  ; -90 to p4 (in dBfs) over the duration defined by p3.
  kamp line -90, p3, p4
  print ampdbfs(p4)
  ; Generate a basic tone using our amplitude value.
a1 oscil ampdbfs(kamp), 440, 1

  ; Since 0dbfs = 1 we don't need to multiply the output
  out a1
endin


</CsInstruments>
<CsScore>

; Table #1, a sine wave.
f 1 0 16384 10 1

; Play Instrument #1 for three seconds.
i 1 0 3 -6
e


</CsScore>
</CsoundSynthesizer>
