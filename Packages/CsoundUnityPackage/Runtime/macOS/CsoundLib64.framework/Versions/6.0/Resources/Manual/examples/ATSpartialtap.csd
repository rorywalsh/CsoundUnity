<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc for RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSpartialtap.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line	0, p3, 2
	ATSbufread ktime, 1, "beats.ats", 30
kfreq1, kam1	ATSpartialtap  5
kfreq2, kam2	ATSpartialtap  20
kfreq3, kam3	ATSpartialtap  30

aout1	oscil	kam1, kfreq1, 1
aout2	oscil	kam2, kfreq2, 1
aout3	oscil	kam3, kfreq3, 1
aout	=	(aout1+aout2+aout3)*10	; amplify some more
	outs	aout, aout

endin


</CsInstruments>
<CsScore>
; sine wave.
f 1 0 16384 10 1
i 1 0 2
e

</CsScore>
</CsoundSynthesizer>
