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

;this instrument is always on and is used to trigger a once off
;jump event. Any time the channel jumpButton changes, JUMP will
;be triggered
instr TriggerInstrument
	kJumpButton chnget "jumpButton"
	if changed(kJumpButton)==1 then
		event "i", "JUMP", 0, 1
	endif
endin

;simple jump sound
instr JUMP
	aEnv expon .5, p3, 0.001
	a1 oscil aEnv, 200
	outs a1, a1
endin


;this instrument is always running. The speed of the player
;determines how many notes are produced each second
instr PLAYER_MOVE
	kSpeed chnget "speedSlider"
	
	if metro(kSpeed*10)==1 then
		kNote randh 30, 1000
		event "i", "PLAY_NOTE", 0, 3, .2, int(abs(kNote))+36
	endif
endin

;this instrument is triggered by the one above and plays a simple enveloped
;sine wave sound. The pitch is choosen at random each time the instrument is
;triggered
instr PLAY_NOTE
	kEnv oscil1 0, p4, p3, 99
	aNoise oscili kEnv, cpsmidinn(p5)
	outs aNoise*chnget:k("speedSlider"), aNoise*chnget:k("speedSlider")
endin



</CsInstruments>
<CsScore>
;envelope for walking sound
f99 0 1024 5 0.1 20 1 100 0.001

;turn on the two triggering instruments
i"TriggerInstrument" 0 [3600*12]
i"PLAYER_MOVE" 0 [3600*12]
</CsScore>
</CsoundSynthesizer>