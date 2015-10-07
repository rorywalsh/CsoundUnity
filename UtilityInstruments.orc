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
SId = p5
kFadeTrigger init 1
kCount init 0

SChannel sprintf "volume%s", SId
chnset p6, SChannel
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

SMessage chnget SChannel
kPan init 1

if changed:k(kStop)==1 then				;stop playback
 		kPlay=0 
 		aRight = 0
 		aLeft = 0
endif
  
if changed:k(kPlay)==1 then
		kPlay = 1
endif
  
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
 
if strindexk(SMessage, "Branch:")!=-1 then
		SNewFile strsubk SMessage, 6, -1
		kPos strindexk SNewFile, "_-_-_"
		SNewChannel strsubk SNewFile, kPos+5, -1
		SNewFile strsubk SNewFile, 1, kPos
 		printf "CurrentID:%s\nNextTrackIs:%s", 1, SChannel, SNewFile 
		kStartBranch = 1
endif 
   
RE_START:
if kPlay==1 then
	;if branching, kill this instrument and schedule a new instance
	iLen filelen SFile
	kCount = (kCount>iLen*sr ? 0 : kCount+ksmps)
	if kCount == 0 && kStartBranch==1 then
		printks "Restarting loop", 0
		String  sprintfk {{i"AudioFilePlayer" 0 360000 "%s" "%s" 1 1}}, SNewFile, SNewChannel  
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
endif

;kFadeLevel FadeIO kFadeTrigger, kFadeTime, kFadeType, kVolume

RE_FADE:

kFadeLevel linseg i(kFadeStartVolume), i(kFadeTime), i(kFadeEndVolume), 1, i(kFadeEndVolume)  

;mixer section
aMixL = ((aLeft*(kPan*2))*kVolume)*kFadeLevel
aMixR = ((aRight*(2-(kPan*2))*kVolume)*kFadeLevel

if kPostSend>0 then
	SChanL sprintf "%s_PostSendL", SChannel
	SChanR sprintf "%s_PostSendR", SChannel
	chnset aMixL*kPostSend, SChanL
	chnset aMixR*kPostSend, SChanR
endif 

if kPreSend>0 then	
	SChanL sprintf "%s_PreSendL", SChannel
	SChanR sprintf "%s_PreSendR", SChannel
	chnset aLeft*kPreSend, SChanL
	chnset aRight*kPreSend, SChanR
endif 

outs ((aLeft*kPan))*kFadeLevel, ((aRight*(1-kPan)))*kFadeLevel

endin
