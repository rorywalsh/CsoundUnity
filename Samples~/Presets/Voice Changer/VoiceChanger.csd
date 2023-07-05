<Cabbage>
form caption("Voice Changer") size(800, 800)
label   bounds  (10, 0, 1000, 100), channel ("main"), text("Main"), fontColour(255, 255, 255, 255), fontSize(30), align(left)
rslider bounds(00, 100, 100, 100), channel("gain"), range(0, 10, 1.0), text("Gain")
rslider bounds(100, 100, 100, 100), channel("cutoff"), range(0, 4000, 600.0), text("Cutoff")
rslider bounds(200, 100, 100, 100), channel("dry"), range(0, 2, 0), text("Dry")
rslider bounds(300, 100, 100, 100), channel("dryReverbSend"), range(0, 2, 0.0), text("Dry Reverb Send")
rslider bounds(400, 100, 100, 100), channel("dryDelaySend"), range(0, 2, 0.0), text("Dry Delay Send")

label bounds  (10, 200, 1000, 100), channel ("Reverb&Delay"), text("Reverb and Delay"), fontColour(255, 255, 255, 255), fontSize(30), align(left)
rslider bounds(0, 300, 100, 100), channel("reverb"), range(0, 5, 0), text("Reverb")
rslider bounds(100, 300, 100, 100), channel("reverbTime"), range(0, 0.95, 0.6), text("Reverb Time")
rslider bounds(200, 300, 100, 100), channel("delay"), range(0, 5, 0), text("Delay")
rslider bounds(300, 300, 100, 100), channel("delayMix"), range(0, 0.996, 0.3), text("Delay Mix")
rslider bounds(400, 300, 100, 100), channel("delayTimeL"), range(0, 0.95, 0.55), text("Delay Time Left")
rslider bounds(500, 300, 100, 100), channel("delayTimeR"), range(0, 0.95, 0.65), text("Delay Time Right")
rslider bounds(600, 300, 100, 100), channel("delayFeedback"), range(0, 0.995, 0.1), text("Delay Feedback")
rslider bounds(700, 300, 100, 100), channel("delayCutoff"), range(0, 0.995, 0.6), text("Delay Cutoff")

label bounds  (10, 400, 1000, 100), channel ("Pitcher"), text("Pitcher"), fontColour(255, 255, 255, 255), fontSize(30), align(left)
rslider bounds(0, 500, 100, 100), channel("pitcherLev"), range(0, 5, 0), text("Pitcher Level")
rslider bounds(100, 500, 100, 100), channel("pitcherScale"), range(0, 4, 0.40), text("Pitcher Scale")
rslider bounds(200, 500, 100, 100), channel("pitcherRevSend"), range(0, 2, 0), text("Pitcher Reverb Send")
rslider bounds(300, 500, 100, 100), channel("pitcherDelaySend"), range(0, 2, 0), text("Pitcher Delay Send")


label bounds  (10, 600, 1000, 100),  channel ("Reverse"), text("Reverse"), fontColour(255, 255, 255, 255), fontSize(30), align(left)
rslider bounds(0, 700, 100, 100), channel("reverseLev"), range(0, 5, 0), text("Reverse Level")
rslider bounds(100, 700, 100, 100), channel("reverseTime"), range(0.3, 2, 0.3), text("Reverse Time")
rslider bounds(200, 700, 100, 100), channel("reverseRevSend"), range(0, 2, 0), text("Reverse Reverb Send")
rslider bounds(300, 700, 100, 100), channel("reverseDelaySend"), range(0, 2, 0), text("Reverse Delay Send")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

; Initialize the global variables. 
sr = 48000
ksmps = 64
nchnls_i = 1
nchnls = 2
0dbfs = 1

giBuffer ftgen  0, 0, 480000, 7, 0; table for audio data storage, 10 seconds buffer
;giCosine	ftgen	0, 0, 8193, 9, 1, 1, 90
;giWin				ftgen	0, 0, 4096, 20, 9, 1									; grain envelope
;giTest ftgen 0, 0, 4096, 10, .1, 0.3, 0.1, 0.1

ga1 init 0      ; audio input
garev init 0    ;reverb output
gaPitcher init 0     ;pitch scaler output
gaReverse init 0   ;reverse output
gaReverbAux init 0   ; an aux that will be used for reverb 
gaDelayAux init 0   ; an aux that will be used for delay

;
;OPCODES 
;

;courtesy Iain McCurdy
opcode    lineto2,k,kk
 kinput,ktime    xin
 ktrig    changed    kinput,ktime    ; reset trigger
 if ktrig==1 then                    ; if new note has been received or if portamento time has been changed...
  reinit RESTART
 endif
 RESTART:                            ; restart 'linseg' envelope
 if i(ktime)==0 then                 ; 'linseg' fails if duration is zero...
  koutput    =    i(kinput)          ; ...in which case output simply equals input
 else
  koutput    linseg    i(koutput),i(ktime),i(kinput)    ; linseg envelope from old value to new value
 endif
 rireturn
         xout    koutput
endop

; from CSOUND MULTI FX
;http://iainmccurdy.org/CsoundRealtimeExamples/Miscellaneous/MultiFX.csd
; Written by Iain McCurdy, 2010
opcode	Reverse, a, aK				;nb. CAPITAL K CREATE A K-RATE VARIABLE THAT HAS A USEFUL VALUE ALSO AT I-TIME
	ain,ktime	xin			;READ IN INPUT ARGUMENTS
	ktrig	changed2	ktime			;IF ktime CONTROL IS MOVED GENERATE A MOMENTARY '1' IMPULSE
	if ktrig=1 then				;IF A TRIGGER HAS BEEN GENERATED IN THE LINE ABOVE...
		reinit	UPDATE			;...BEGIN A REINITILISATION PASS FROM LABEL 'UPDATE'
	endif					;END OF CONDITIONAL BRANCH
	UPDATE:					;LABEL CALLED 'UPDATE'
	itime	=	i(ktime)		;CREATE AN I-TIME VERSION OF ktime
	;prints "itime: %f, %f\n", itime, ktime
	aptr	phasor	2/itime			;CREATE A MOVING PHASOR THAT WITH BE USED TO TAP THE DELAY BUFFER
	aptr	=	aptr*itime		;SCALE PHASOR ACCORDING TO THE LENGTH OF THE DELAY TIME CHOSEN BY THE USER
	ienv	ftgentmp	0,0,1024,7,0,(1024*0.01),1,(1024*0.98),1,(0.01*1024),0	;ANTI-CLICK ENVELOPE SHAPE
 	aenv	poscil	1, 2/itime, ienv	;CREATE A CYCLING AMPLITUDE ENVELOPE THAT WILL SYNC TO THE TAP DELAY TIME PHASOR 
 	abuffer	delayr	itime			;CREATE A DELAY BUFFER
	atap	deltap3	aptr			;READ AUDIO FROM A TAP WITHIN THE DELAY BUFFER
		delayw	ain			;WRITE AUDIO INTO DELAY BUFFER
	rireturn				;RETURN FROM REINITIALISATION PASS
	xout	atap*aenv			;SEND AUDIO BACK TO CALLER INSTRUMENT. APPLY AMPLITUDE ENVELOPE TO PREVENT CLICKS.
endop

opcode	AnalogDelay, a, akkkk
	ain,kmix,ktime,kfback,ktone	xin			;READ IN INPUT ARGUMENTS
	ktone	expcurve	ktone,4				;CREATE AN EXPONENTIAL REMAPPING OF ktone
	ktone	scale	ktone,12000,100				;RESCALE 0 - 1 VALUE
	iWet	ftgentmp	0,0,1024,-7,0,512,1,512,1	;RESCALING FUNCTION FOR WET LEVEL CONTROL
	iDry	ftgentmp	0,0,1024,-7,1,512,1,512,0	;RESCALING FUNCTION FOR DRY LEVEL CONTROL
	kWet	table	kmix, iWet, 1				;RESCALE WET LEVEL CONTROL ACCORDING TO FUNCTION TABLE iWet
	kDry	table	kmix, iDry, 1                 		;RESCALE DRY LEVEL CONTROL ACCORDING TO FUNCTION TABLE iWet
	kporttime	linseg	0,0.001,0.1			;RAMPING UP PORTAMENTO TIME
	kTime	portk	ktime, kporttime*3			;APPLY PORTAMENTO SMOOTHING TO DELAY TIME PARAMETER
	kTone	portk	ktone, kporttime			;APPLY PORTAMENTO SMOOTHING TO TONE PARAMETER
	aTime	interp	kTime					;INTERPOLATE AND CREAT A-RATE VERSION OF DELAY TIME PARAMETER
	aBuffer	delayr	5					;READ FROM (AND INITIALIZE) BUFFER
	atap	deltap3	aTime					;TAP DELAY BUFFER
	atap	clip	atap, 0, 0dbfs				;SIGNAL IS CLIPPED AT MAXIMUM AMPLITUDE USING BRAM DE JONG METHOD
	atap	tone	atap, kTone				;LOW-PASS FILTER DELAY TAP WITHIN DELAY BUFFER 
		delayw	ain+(atap*kfback)			;WRITE INPUT AUDIO AND FEEDBACK SIGNAL INTO DELAY BUFFER
	aout	sum	ain*kDry, atap*kWet			;MIX DRY AND WET SIGNALS 
		xout	aout					;SEND AUDIO BACK TO CALLER INSTRUMENT
endop

;
; MAIN INSTR
;
instr 1

; get current gain
kGain chnget "gain"
kCutoff chnget "cutoff"
kDry chnget "dry"
kRev chnget "reverb"
kRevTime chnget "reverbTime"
kDel chnget "delay"
kDelMix chnget "delayMix"
kDelTimeL chnget "delayTimeL"
kDelTimeR chnget "delayTimeR"
kDelFbk chnget "delayFeedback"
kDelCutoff chnget "delayCutoff"

kDryRevSend chnget "dryReverbSend"
kDryDelaySend chnget "dryDelaySend"

kGain lineto2 kGain, 0.01
kCutoff lineto2 kCutoff, 0.01
kDry lineto2 kDry, 0.01
kRev lineto2 kRev, 0.01
kRevTime lineto2 kRevTime, 0.01
kDel lineto2 kDel, 0.01
kDelMix lineto2 kDelMix, 0.01
kDelTimeL lineto2 kDelTimeL, 0.01
kDelTimeR lineto2 kDelTimeR, 0.01
kDelFbk lineto2 kDelFbk, 0.01
kDelCutoff lineto2 kDelCutoff, 0.01

kDryRevSend lineto2 kDryRevSend, 0.01
kDryDelaySend lineto2 kDryDelaySend, 0.01


giTableLen  =       ftlen(giBuffer)  ; derive buffer function table length
gidur       =       giTableLen / sr   ; derive storage time in seconds

; get input channel
ga1 in 
;ga1 oscili 0.4, 440, 1 ; test signal

; filter the input 
ga1 tone ga1, kCutoff

vincr gaReverbAux, ga1 * kDryRevSend
vincr gaDelayAux, ga1 * kDryDelaySend

;printks "dry delay send: %f", 0.1, kDryDelaySend
;write to a table to be used by partikkel in instr 3
;andx phasor (sr / giTableLen)
;tablew   ga1, andx, giBuffer, 1 ; write audio to function table
;printks "ilen: %d, aindx: %f\n", 0.1, giTableLen, k(andx)
;abuf table andx, giBuffer, 1

denorm gaReverbAux  
;aRevL, aRevR freeverb gaccum, gaccum, kRevTime, 0.5
aRevL, aRevR reverbsc gaReverbAux, gaReverbAux, kRevTime, 8000, sr, 0.5, 1

;aDelTimeL upsamp kDelTimeL
;aDelTimeR upsamp kDelTimeR

;aDelL vdelay gaDelayAux, aDelTimeL * 1000, 5000
;aDelR vdelay gaDelayAux, aDelTimeR * 1000, 5000

aDelL AnalogDelay gaDelayAux, kDelMix, kDelTimeL, kDelFbk, kDelCutoff
aDelR AnalogDelay gaDelayAux, kDelMix, kDelTimeR, kDelFbk, kDelCutoff

aoutL sum ga1 * kDry, gaPitcher, gaReverse, aRevL * kRev, aDelL * kDel;, abuf
aoutR sum ga1 * kDry, gaPitcher, gaReverse, aRevR * kRev, aDelR * kDel

clear	gaReverbAux
clear   gaDelayAux

outs aoutL, aoutR

endin

;
; PITCH SCALER
;
instr 2

kpitcherScale chnget "pitcherScale"
klev chnget "pitcherLev"
kRevSend chnget "pitcherRevSend"
kDelaySend chnget "pitcherDelaySend"

kpitcherScale lineto2 kpitcherScale, 0.01
klev lineto2 klev, 0.01
kRevSend lineto2 kRevSend, 0.01
kDelaySend lineto2 kDelaySend, 0.01

gifftsize =         1024
gioverlap =         gifftsize / 4
giwinsize =         gifftsize
giwinshape =        1; von-Hann window

prints "pitch: %d", kpitcherScale
fftin     pvsanal  ga1, gifftsize, gioverlap, giwinsize, giwinshape
fftscal   pvscale  fftin, kpitcherScale
gaPitcher      pvsynth  fftscal

vincr gaReverbAux, gaPitcher * kRevSend
vincr gaDelayAux, gaPitcher * kDelaySend

gaPitcher *= klev

endin

;
; REVERSER
;
instr 3 
kRvrsTime init 1
 
kRvrsLev chnget "reverseLev"
kRvrsTime chnget "reverseTime"
kRvrsRevSend chnget "reverseRevSend"
kRvrsDelSend chnget "reverseDelaySend"

kRvrsLev lineto2 kRvrsLev, 0.01
kRvrsRevSend lineto2 kRvrsRevSend, 0.01
kRvrsDelSend lineto2 kRvrsDelSend, 0.01

gaReverse Reverse ga1, kRvrsTime

vincr gaReverbAux, gaReverse * kRvrsRevSend
vincr gaDelayAux, gaReverse * kRvrsDelSend

gaReverse *= kRvrsLev
    
;printks "kRvrsTime: %f\n", 0.1, kRvrsTime
;ktest = (kRvrsTime > 0) ? 1 : 0
;prints "kTest: %d\n\n", ktest 
;if (ktest == 1) then
;    gaReverse Reverse ga1, kRvrsTime
;    vincr gaReverbAux, gaReverse * kRvrsLev
;    gaReverse *= kRvrsLev
;else
;    prints "reverse time cannot be 0: %f\n", kRvrsTime
;endif


endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
f1 0 [4096^2] 10 .1 0.3 0.1 0.1
;starts instruments and runs them for 7000 years too!
i1 0.1 z 
i2 0.1 z
i3 0.2 z
</CsScore>
</CsoundSynthesizer>
