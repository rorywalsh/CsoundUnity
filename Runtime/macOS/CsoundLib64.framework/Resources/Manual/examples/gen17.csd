<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac  -M0 -+rtmidi=virtual    ;;;realtime audio out
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o gen17.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100 
ksmps = 32 
nchnls = 2 
0dbfs  = 1 

 instr 1
 
inote  cpsmidi	 
iveloc ampmidi .5
ictl   midictrl 5				;move slider of controller 5 to change ftable
itab   table ictl, 2
aout   poscil iveloc, inote, itab
       outs aout, aout

endin	 
</CsInstruments>
<CsScore>
f 1 0 8193 10 1
f 2 0 128 -17 0 10 32 20 64 30 96 40			;inhibit rescaling

f 10 0 16384 10 1                                       ; Sine
f 20 0 16384 10 1 0.5 0.3 0.25 0.2 0.167 0.14 0.125 .111; Sawtooth
f 30 0 16384 10 1 0   0.3 0    0.2 0     0.14 0     .111; Square
f 40 0 16384 10 1 1   1   1    0.7 0.5   0.3  0.1       ; Pulse

f 0 30	;run for 30 seconds
e
</CsScore>
</CsoundSynthesizer>

