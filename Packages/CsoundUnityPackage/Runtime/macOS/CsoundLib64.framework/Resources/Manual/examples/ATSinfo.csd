<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2

instr 1	; "fox.ats" is created by atsa

inum_partials	ATSinfo	"fox.ats", 3
		print	inum_partials 

endin

</CsInstruments>
<CsScore>
i 1 0 0 
e

</CsScore>
</CsoundSynthesizer>
