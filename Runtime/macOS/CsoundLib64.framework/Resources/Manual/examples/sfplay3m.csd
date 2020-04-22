<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual -M0 ;;;realtime audio out
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o sfplay3m.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100 
ksmps = 32
nchnls = 2
0dbfs  = 1 


gisf	sfload	"07AcousticGuitar.sf2"
	sfplist	gisf
	sfpassign 10, gisf

instr 1	; play from score and midi keyboard

	mididefault	60, p3
	midinoteonkey	p4, p5
inum	init	p4
ivel	init	p5
ivel	init	ivel/127					;make velocity dependent
kamp	linsegr	1, 1, 1, .1, 0
kamp	= kamp/7000						;scale amplitude
kfreq	init	1						;do not change freq from sf
aout	sfplay3m ivel, inum, kamp*ivel, kfreq, 10		;preset index = 10
	outs	aout, aout
	
endin
	
</CsInstruments>
<CsScore>
f0  60	; stay active for 1 minute

i1 0 1 60 127
i1 + 1 62 <
i1 + 1 65 <
i1 + 1 69 10

e
</CsScore>
</CsoundSynthesizer>
