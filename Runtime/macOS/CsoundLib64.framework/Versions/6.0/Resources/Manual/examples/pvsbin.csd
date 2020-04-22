<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in
-odac           -iadc    ;;;RT audio I/O
; For Non-realtime ouput leave only the line below:
; -o pvsbin.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
kr = 4410
ksmps = 10
nchnls = 1

instr 1
ifftsize = 1024  
iwtype = 1    /* cleaner with hanning window */

;a1   soundin "input.wav"  ;select a soundifle
a1 inch 1   ;Use realtime input

fsig pvsanal   a1, ifftsize, ifftsize/4, ifftsize, iwtype
kamp, kfr pvsbin   fsig, 10
adm  oscil     kamp, kfr, 1

       out    adm
endin

</CsInstruments>
<CsScore>
; sine wave
f 1 0 4096 10 1

i 1 0 30
e

</CsScore>
</CsoundSynthesizer>
