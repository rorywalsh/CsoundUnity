<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac -+rtmidi=virtual -M0   ;;;realtime audio out, virtual midi in
;-iadc    ;;;uncomment -iadc if RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o sfload.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1

;load two soundfonts
isf	sfload	"07AcousticGuitar.sf2"
ir	sfload	"01hpschd.sf2"
	sfplist isf
	sfplist ir
	sfpassign	0, isf	
	sfpassign	1, ir

instr 1	; play guitar from score and midi keyboard - preset index = 0

	mididefault	60, p3
	midinoteonkey	p4, p5
inum	init	p4
ivel	init	p5
ivel	init	ivel/127					;make velocity dependent
kamp	linsegr	1, 1, 1, .1, 0
kamp	= kamp/3000						;scale amplitude
kfreq	init	1						;do not change freq from sf
a1,a2	sfplay3	ivel, inum, kamp*ivel, kfreq, 0			;preset index = 0
	outs	a1, a2
	
	endin
	
instr 2	; play harpsichord from score and midi keyboard - preset index = 1

	mididefault	60, p3
	midinoteonkey	p4, p5
inum	init	p4
ivel	init	p5
ivel	init	ivel/127					;make velocity dependent
kamp	linsegr	1, 1, 1, .1, 0
kamp	= kamp/1000						;scale amplitude
kfreq	init	1						;do not change freq from sf
a1,a2	sfplay3	ivel, inum, kamp*ivel, kfreq, 1			;preset index = 1
	outs	a1, a2
	
endin
	
</CsInstruments>
<CsScore>
f0  60				; stay active for 1 minute

i1 0 1 60 100
i1 + 1 62 <
i1 + 1 65 <
i1 + 1 69 10

i2 5 1 60 100
i2 + 1 62 <
i2 7 1 65 <
i2 7 1 69 10

e
</CsScore>
</CsoundSynthesizer>
