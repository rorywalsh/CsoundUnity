<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual  -M0  ;;;realtime audio I/O with MIDI in
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1

gisine	ftgen 0, 0, 1024, 10, 1

instr 1

ivel veloc 0, 1				;scale 0 - 1
print ivel				;print velocity
asig poscil .5*ivel, 220, gisine	;and use it as amplitude
     outs asig, asig
       
endin
</CsInstruments>
<CsScore>

f 0 30     ;runs 30 seconds

e
</CsScore>
</CsoundSynthesizer>

