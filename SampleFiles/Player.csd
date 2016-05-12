<Cabbage>
form caption("Character Sounds"), size(300, 200)
button bounds(8, 8, 60, 25), channel("jumpButton"), text("Jump")
hslider bounds(8, 80, 280, 30), channel("speedSlider"), text("Speed"), range(0, 1, 0)
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

;this instrument can be used to trigger once off events
;such as jump or collision
instr TriggerInstrument
	kJumpButton chnget "jumpButton"
	if changed(kJumpButton)==1 then
		event "i", "JUMP", 0, 1
	endif
endin

instr PLAYER_MOVE
	aNoise buzz .4, 100*chnget:k("speedSlider"), 3, -1
	outs aNoise*chnget:k("speedSlider"), aNoise*chnget:k("speedSlider")
endin

instr JUMP
prints "Jumpy"
aEnv expon .5, p3, 0.001
a1 oscil aEnv, 200
outs a1, a1
endin

</CsInstruments>
<CsScore>
f100 0 8 -2 64 60 67 0 65 0 67 48
f0 [60*60*24*7]
i"TriggerInstrument" 0 [3600*12]
i"PLAYER_MOVE" 0 [3600*12]
</CsScore>
</CsoundSynthesizer>