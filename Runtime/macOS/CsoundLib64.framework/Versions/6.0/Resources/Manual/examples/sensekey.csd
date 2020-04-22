<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in
-odac           -iadc    ;;;RT audio I/O
; For Non-realtime ouput leave only the line below:
; -o sensekey.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

; Initialize the global variables.
sr = 44100
kr = 4410
ksmps = 10
nchnls = 1

; Instrument #1.
instr 1
  k1 sensekey
  printk2 k1
endin


</CsInstruments>
<CsScore>

; Play Instrument #1 for thirty seconds.
i 1 0 30
e


</CsScore>
</CsoundSynthesizer>
