nchnls = 2

;===================================
;==== SIMPLE FADE IN/OUT OPCODE ====
;===================================
giFadeIn ftgen 1, 0, 16384, 7, 0, 16384, 1
giFadeOut ftgen 2, 0, 16384, 7, 1, 16384, 0

opcode FadeIO, k, kkkk
kTrigger, kTime, kType, kVol xin
setksmps 1
kIndex init 0
kOut init 1
kRamp init 0
if changed:k(kTrigger)==1 then
	kRamp = 1
endif

if kRamp==1 then 
	if kType==0 && kTime>0 then
	kOut tab kIndex/(kTime*sr), giFadeIn, 1
	elseif kType==1 && kTime>0 then
	kOut tab kIndex/(kTime*sr), giFadeOut, 1
	elseif kType==0 && kTime==0 then
	kOut = 1
	elseif kType==1 && kTime==0 then
	kOut = 0
	else
	kOut = 1
	endif 
	kIndex = kIndex+1
	if kIndex>=kTime*sr then
		kRamp=0
		kIndex = 0
	endif
endif

xout kOut*kVol
endop

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
SChannel = p5
SFile = p4
SMessage chnget SChannel  

kMessageUpdated changed SMessage
SVolume init "0"
SFade init "0"
aLeft, aRight init 0
kPan init .5;

if kMessageUpdated==1 then     
	if strindexk(SMessage, "Volume:")!=-1 then
 		SVolume strsubk SMessage, 7, -1
 		SVolume strcatk "0", SVolume
 		kVolume strtodk SVolume
 		printks "Volume:%f", 0, kVolume
 	elseif strindexk(SMessage, "FadeIn:")!=-1 then
 		SFade strsubk SMessage, 7, -1
 		SFade strcatk "0", SFade
 		kFadeTime strtodk SFade
 		;printks "FadeIn:%f", 0, kFadeTime 
 		kFadeType = 0
 		kFadeTrigger randh 100, sr, 2
 		kPlay = 1
 	elseif strindexk(SMessage, "FadeOut:")!=-1 then
 		SFade strsubk SMessage, 8, -1
 		SFade strcatk "0", SFade 
 		kFadeTime strtodk SFade
 		;printks "FadeOut:%f", 0, kFadeTime 
 		kFadeType = 1
 		kFadeTrigger randh 100, sr, 2
 		kPlay = 1
 	elseif strindexk(SMessage, "Speed:")!=-1 then
 		SSpeed strsubk SMessage, 6, -1
 		SSpeed strcatk "0", SSpeed 
 		kSpeed strtodk SSpeed
 		printks "Speed:%f", 0, kFadeTime 
 	elseif strindexk(SMessage, "Branch:")!=-1 then
		SNewFile strsubk SMessage, 6, -1
 		;SNewFile strcatk " ", SNewFile 
		kPos strindexk SNewFile, "_-_-_"
		SNewChannel strsubk SNewFile, kPos+5, -1
		SNewFile strsubk SNewFile, 1, kPos
 		printf "CurrentID:%s\nNextTrackIs:%s", 1, SChannel, SNewFile 
		kStartBranch = 1
  	elseif strindexk(SMessage, "Play")!=-1 then
 		kPlay=1
 		kFadeTrigger randh 100, sr, 2
 		kFadeType = 0
 		kFadeTime = 0
 		SVolume strsubk SMessage, 4, -1
 		SVolume strcatk "0", SVolume
 		kVolume strtodk SVolume
 		printks "Play:%f", 0, kVolume 		
		reinit RE_INIT
	elseif strcmpk(SMessage, "Stop")==0 then
 		kPlay=0 
 		kFade = 1
 		aRight = 0
 		aLeft = 0
 	elseif strindexk(SMessage, "PreSend:")!=-1 then
 		SSend strsubk SMessage, 8, -1
 		SSend strcatk "0", SSend
 		kPreSend strtodk SSend
 		printks "PreSend:%f", 0, kPreSend
 	elseif strindexk(SMessage, "PostSend:")!=-1 then
 		SSend strsubk SMessage, 9, -1
 		SSend strcatk "0", SSend
 		kPostSend strtodk SSend
 		printks "PostSend:%f", 0, kPostSend
 	endif
endif

kPlay init p7
kVolume init p6
kFadeLevel init 1
kSpeed init 1
kPreSend init 0
kPostSend init 0
kFadeTime init 0
kFadeTrigger init 0
kFadeType init 0
kFadeCnt init 0
kCount init 0
kStartBranch init 0

RE_INIT:
if kPlay==1 then
	iLen filelen SFile
	kCount = (kCount>iLen*sr ? 0 : kCount+ksmps)
	if kCount == 0 && kStartBranch==1 then
		printks "Restarting loop", 0
		;event "i", "AudioFilePlayer", 0, 1000, gkNextLoop
		String  sprintfk {{i"AudioFilePlayer" 0 360000 "%s" "%s" 1 1}}, SNewFile, SNewChannel  
		scoreline String, 1

		;event "i\"AudioFilePlayer\" 0 360000 \""+audioFile+"\" \""+ID+"\" "+volume.ToString()+" "+ shouldPlay.ToString());

		gkStartBranch=0

		turnoff 
	endif

	iChannels filenchnls p4
	if iChannels==1 then
		aLeft diskin2 p4, kSpeed, 0, 1
		aRight = aLeft
		iPan = .5	;panning only enabled on mono sources
	else
		aLeft, aRight diskin2 p4, kSpeed, 0, 1
	endif
endif

;print some info about instantiation
SInfo sprintf "Initialising audio file: %s\nChannel ID is: %s, Should play: %d, Volume is: %d\n", SFile, SChannel, kPlay, kVolume
prints SInfo


kFadeLevel FadeIO kFadeTrigger, kFadeTime, kFadeType, kVolume

aMixL = ((aLeft*kPan)*kVolume)*kFadeLevel
aMixR = ((aRight*(1-kPan))*kVolume)*kFadeLevel

if kPostSend>0 then
	SChanL sprintf "%s_PostSendL", SChannel
	SChanR sprintf "%s_PostSendR", SChannel

	printks SChanR, .5
	chnset aMixL*kPostSend, SChanL
	chnset aMixR*kPostSend, SChanR
endif 

if kPreSend>0 then	
	SChanL sprintf "%s_PreSendL", SChannel
	SChanR sprintf "%s_PreSendR", SChannel
	chnset aLeft*kPreSend, SChanL
	chnset aRight*kPreSend, SChanR
endif 

outs ((aLeft*kPan)*kVolume)*kFadeLevel, ((aRight*(1-kPan))*kVolume)*kFadeLevel

endin

