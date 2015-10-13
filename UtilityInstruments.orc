nchnls = 2
0dbfs=1

;=======================================
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

;=======================================
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

;=======================================
;PLAY WITH CHANNEL CONTROL
;p4 = soundfile
;p5 = channel
;chn_S "loop1", 1
;chn_S "loop2", 1
;chn_S "loop3", 1
;chn_S "loop4", 1
;chn_S "loop5", 1

instr AudioFilePlayer
aLeft, aRight init 0
SFile = p4
SId strcpy p5
;prints p4
;prints p5
SId = p6
;print p7
kFadeTrigger init 1
kCount init 0
kSpeed init 1
kStartBranch init 0

SStringChannel1 sprintf "branch%s", SId
chn_S SStringChannel1, 3
SNewBranchFile chnget SStringChannel1

SStringChannel2 sprintf "newBranchId%s", SId
chn_S SStringChannel2, 3
SNewBranchID chnget SStringChannel2

SChannel sprintf "volume%s", SId
chnset p8, SChannel
kVolume chnget SChannel

SChannel sprintf "play%s", SId
chnset p7, SChannel
kPlay chnget SChannel 
 
SChannel sprintf "speed%s", SId
chnset 1, SChannel
kSpeed chnget SChannel

SChannel sprintf "stop%s", SId
chnset 0, SChannel
kStop chnget SChannel

SChannel sprintf "fadeIn%s", SId
chnset 0, SChannel
kFadeIn chnget SChannel

SChannel sprintf "fadeOut%s", SId
chnset 0, SChannel
kFadeOut chnget SChannel

kFadeTime init 1
SChannel sprintf "fadeTime%s", SId
chnset 1, SChannel
kFadeTime chnget SChannel

SChannel sprintf "restart%s", SId
chnset 0, SChannel
kRestart chnget SChannel

kFadeStartVolume init 1
SChannel sprintf "fadeStartVolume%s", SId
chnset 1, SChannel
kFadeStartVolume chnget SChannel

kFadeEndVolume init 1
SChannel sprintf "fadeEndVolume%s", SId
chnset 1, SChannel
kFadeEndVolume chnget SChannel

SChannel sprintf "postSend%s", SId
kPostSend chnget SChannel

SChannel sprintf "preSend%s", SId
kPreSend chnget SChannel


kPan init 1
SInfo sprintf "Initialising audio file: %s\nChannel ID is: %s, Should play: %d, Volume is: %d\n", SFile, SId, p7, kVolume
prints SInfo

 
if changed:k(kFadeIn)==1 then 	;start fade in
		if kFadeEndVolume==-1 then
			kFadeEndVolume = kVolume
		endif 
			
		kFadeStartVolume = 0
		if kRestart==1 then
			reinit RE_START
		endif
		reinit RE_FADE 
endif

if changed:k(kFadeOut)==1 then 	;start fade out
		if kFadeStartVolume==-1 then
			kFadeStartVolume = kVolume
		endif
		
		;kFadeStartVolume = kVolume	
		;kFadeEndVolume = 0	
		if kRestart==1 then
			reinit RE_START
		endif
		reinit RE_FADE 
endif

;kBranchUpdate changed SBranch

if changed:k(SNewBranchFile)==1 then
		kStartBranch = 1
endif 

RE_START:
if kPlay==1 then
	;if branching, kill this instrument and schedule a new instance
	iLen filelen SFile
	kCount = (kCount>iLen*sr ? 0 : kCount+ksmps)
	if kCount == 0 && kStartBranch==1 then
		printks "Restarting loop", 0
		String  sprintfk {{i"AudioFilePlayer" 0 360000 "%s" "empty" "%s" 1 1}}, SNewBranchFile, SNewBranchID  
		scoreline String, 1
		gkStartBranch=0
		turnoff 
	endif

	iChannels filenchnls p4
	if iChannels==1 then
		aLeft diskin2 p4, kSpeed, 0, 1
		aRight = aLeft
		iPan = .5
	else
		aLeft, aRight diskin2 p4, kSpeed, 0, 1
	endif
else
	aLeft = 0 
	aRight = 0
endif
;kFadeLevel FadeIO kFadeTrigger, kFadeTime, kFadeType, kVolume

RE_FADE:

kFadeLevel linseg i(kFadeStartVolume), i(kFadeTime), i(kFadeEndVolume), 1, i(kFadeEndVolume)  

;mixer section
aMixL = ((aLeft)*kVolume)*kFadeLevel
aMixR = ((aRight)*kVolume)*kFadeLevel

if kPostSend>0 then  
	SChanL sprintf "%s_PostSendL", SId
	SChanR sprintf "%s_PostSendR", SId
	chnset aMixL*kPostSend, SChanL
	chnset aMixR*kPostSend, SChanR
endif 

if kPreSend>0 then	
	SChanL sprintf "%s_PreSendL", SId
	SChanR sprintf "%s_PreSendR", SId
	chnset aLeft*kPreSend, SChanL
	chnset aRight*kPreSend, SChanR
endif 

outs aLeft*kFadeLevel, aRight*kFadeLevel

endin