<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in
-odac           -iadc    ;;;RT audio I/O
; For Non-realtime ouput leave only the line below:
; -o pvspitch.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 128
nchnls = 1


giwave ftgen 0, 0, 4096, 10, 1, 0.5, 0.333, 0.25, 0.2, 0.1666 

instr 1

ifftsize = 1024
iwtype = 1    /* cleaner with hanning window */

a1 inch 1 ;Realtime audio input
;a1   soundin "input.wav" ;Use this line for file input

fsig pvsanal   a1, ifftsize, ifftsize/4, ifftsize, iwtype
kfr, kamp pvspitch   fsig, 0.01

adm  oscil     kamp, kfr * 1.5, giwave  ;Generate note a fifth above detected pitch

       out    adm
endin


</CsInstruments>
<CsScore>

i 1 0 30

e

</CsScore>
</CsoundSynthesizer>
