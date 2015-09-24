nchnls = 2
;PLAY ONE SHOT
;p4 = soundfile
;p5 = volume
;p6 = pan
;p7 = pitch
instr PlayOneShot
aLeft, aRight init 0
iPan init .5
iVal init 1
iLenght filelen p4
p3 = iLenght
iChannels filenchnls p4
if iChannels==1 then
	aLeft diskin2 p4, p7
	aRight = aLeft
	iPan = p6	;panning only enabled on mono sources
else
	aLeft, aRight diskin2 p4, p7
endif
outs (aLeft*iPan)*p5, (aRight*(1-0))*p5
endin

;PLAY LOOPED
;p4 = soundfile
;p5 = volume
;p6 = pan
;p7 = pitch
instr PlayLooped
aLeft, aRight init 0
iPan init .5;
iChannels filenchnls p4
if iChannels==1 then
	aLeft diskin2 p4, p7, 0, 1
	aRight = aLeft
	iPan = p6	;panning only enabled on mono sources
else
	aLeft, aRight diskin2 p4, p7, 0, 1
endif
outs (aLeft*iPan)*p5, (aRight*(1-0))*p5
endin
