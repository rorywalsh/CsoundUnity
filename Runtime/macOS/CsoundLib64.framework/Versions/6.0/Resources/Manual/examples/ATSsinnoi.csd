<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc if RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSsinnoi.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line	0,  p3, 2
knzfade	expon	0.001, p3, 2
aout	ATSsinnoi 	ktime, 1, knzfade, 1, "beats.ats", 150
	outs	aout*2, aout*2		;amplify some more
endin

</CsInstruments>
<CsScore>

i 1 0 2 
e

</CsScore>
</CsoundSynthesizer>
