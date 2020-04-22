<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual -M0 ;;;realtime audio out and midi in
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o scantable.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1

gi1 ftgen 1, 0, 128, 7, 0, 64, 1, 64, 0		; initial position
gi2 ftgen 2, 0, 128, -7, 1, 128, 1		; masses
gi3 ftgen 3, 0, 128, -7, 0, 64, 100, 64, 0	; stiffness
gi4 ftgen 4, 0, 128, -7, 1, 128, 1		; damping
gi5 ftgen 5, 0, 128, -7, 0, 128, 0.5		; initial velocity


instr 1

iamp ampmidi .5
ipch cpsmidi 
kenv madsr .1, .1, .8, .3

asig scantable iamp, ipch, 1, 2, 3, 4, 5
asig dcblock asig
     outs asig*kenv, asig*kenv

endin
</CsInstruments>
<CsScore>

f0 60	; play for 60 seconds
e
</CsScore>
</CsoundSynthesizer>
