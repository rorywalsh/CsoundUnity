<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc for RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSadd.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line	0, p3, 2
asig	ATSadd	ktime, 1,  "beats.ats", 1, 20, 0, 2
	outs	asig*3, asig*3	;amplify

endin


</CsInstruments>
<CsScore>
;sine wave.
f 1 0 16384 10 1

i 1 0 2 
e

</CsScore>
</CsoundSynthesizer>
