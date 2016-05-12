<Cabbage>
form caption("Cube Sound"), size(300, 200)
hslider bounds(8, 40, 280, 30), channel("cubeVolumeSlider"), text("Cube Sound"), range(0, 1, 0)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d -m0d
</CsOptions>
<CsInstruments>
sr 	= 	48000 
ksmps 	= 	32
nchnls 	= 	2
0dbfs	=	1 


instr SimpleMelody
kNoteCount init 0
iNoteTable = 100

if metro(4)==1 then
	kNote tab kNoteCount, iNoteTable
	if kNote!=0 then
		event "i", "PLAY_NOTE", 0, 1, kNote
	endif
	kNoteCount = kNoteCount==7 ? 0 : kNoteCount+1 
endif

endin

instr PLAY_NOTE
iTable = 101
aEnv linen 1, p3*.25, p3, p3*.75 
a1 oscil aEnv, cpsmidinn(p4), iTable
outs a1*chnget:k("cubeVolumeSlider"), a1*chnget:k("cubeVolumeSlider")
endin

</CsInstruments>
<CsScore>
f100 0 8 -2 60 0 60 0 72 72 72 60
f101 0 4096 10 1 .5 .25
f0 [60*60*24*7]
i"SimpleMelody" 0 [3600*12]
</CsScore>
</CsoundSynthesizer>