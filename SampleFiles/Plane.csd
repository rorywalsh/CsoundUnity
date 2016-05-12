<Cabbage>
form caption("Simple Melody"), size(300, 200)
hslider bounds(8, 40, 280, 30), channel("melodyVolumeSlider"), text("Melody"), range(0, 1, 0)
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

if metro(1)==1 then
	kNote tab kNoteCount, iNoteTable
	if kNote!=0 then
		event "i", "PLAY_NOTE", 0, 2, kNote
	endif
	kNoteCount = kNoteCount==7 ? 0 : kNoteCount+1 
endif

endin

instr PLAY_NOTE
aEnv linen 1, p3*.25, p3, p3*.75 
a1 oscil aEnv, cpsmidinn(p4)
outs a1*chnget:k("melodyVolumeSlider"), a1*chnget:k("melodyVolumeSlider")
endin

</CsInstruments>
<CsScore>
f100 0 8 -2 64 60 67 0 65 0 67 48
f0 [60*60*24*7]
i"SimpleMelody" 0 [3600*12]
</CsScore>
</CsoundSynthesizer>