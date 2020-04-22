<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc for RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSaddnzwav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line     0, p3, 2
asig	ATSaddnz ktime, "cage.ats", 1, 24
	outs	asig*10, asig*10	;amplify
endin

</CsInstruments>
<CsScore>

i 1 0 2 
e

</CsScore>
</CsoundSynthesizer>