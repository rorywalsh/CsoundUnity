<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc if RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSreadnz.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line	0, p3, 2
kenergy	ATSreadnz ktime, "beats.ats", 2
anoise	randi	kenergy, 500
aout	oscili	0.005, 455, 1
aout	=	aout * anoise
	outs	aout, aout 
endin

</CsInstruments>
<CsScore>
; cosine wave
f 1 0 16384 11 1 1

i 1 0 2 
e

</CsScore>
</CsoundSynthesizer>
