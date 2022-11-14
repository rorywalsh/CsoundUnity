<Cabbage> bounds(0, 0, 0, 0)
form caption("Partikkel-2, by J. Heintz and O. Brandtsegg") size(600, 300) pluginId("GRNS")
rslider bounds(10, 10, 100, 100) range(0, 2.0, 1, 1, 0.001) text("Gain") channel("gain")
rslider bounds(110, 10, 100, 100) range(-8, 8, 1, 1, 0.001) text("Speed") channel("speed")
rslider bounds(210, 10, 100, 100) range(0.05, 500, 200, 1, 0.001) text("Density") channel("density")
rslider bounds(310, 10, 100, 100) range(1, 2000, 15, 1, 0.001) text("GrainSize ms") channel("grainSize")
rslider bounds(410, 10, 100, 100) range(-1200, 1200, 0, 1, 0.001) text("Grain Pitch") channel("grainPitch")
rslider bounds(10, 110, 100, 100) range(-1200, 1200, 0, 1, 0.001) text("Random Pitch") channel("randPitch")
rslider bounds(110, 110, 100, 100) range(-8, 8, 1, 1, 0.001) text("Jitter") channel("jitter")
rslider bounds(210, 110, 100, 100) range(-8, 8, 0, 1, 0.001) text("Distribution") channel("distribution")
rslider bounds(310, 110, 100, 100) range(0, 1, 1, 1, 0.001) text("Spread") channel("spread")
rslider bounds(410, 110, 100, 100) range(0.1, 50, 10, 1, 0.001) text("Panning Duration") channel("panDur")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out  
-odac           	;;;RT audio 
; For Non-realtime ouput leave only the line below:
; this won't work in Unity: if you want to record output use a recorder instrument
; -o partikkel.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>
; opcode needed for the amplitude meters
opcode vuMeter, k, a
ain xin
ametera1	follow 		ain, .01		;CREATE AN AMPLITUDE FOLLOWING UNIPOLAR SIGNAL (NEEDED FOR THE AMPLITUDE METERS)
kmetera1	downsamp	ametera1		;CONVERT AMPLITUDE FOLLOWING SIGNAL TO K-RATE
kmetera1	portk		kmetera1, .1	;SMOOTH THE MOVEMENT OF THE AMPLITUDE FOLLOWING SIGNAL - THIS WILL MAKE THE METERS EASIER TO VIEW
xout kmetera1
endop

;sr = 44100    ; setting the sampling rate is useless since Unity uses the AudioSettings.SampleRate
ksmps = 32
nchnls = 8

; Example by Joachim Heintz and Oeyvind Brandtsegg 2008
; ported to CsoundUnity (and slightly mixed with partikkel-panlaws.csd) by gb 2021

giCosine			ftgen	0, 0, 8193, 9, 1, 1, 90									; cosine
giDisttab			ftgen	0, 0, 32768, 7, 0, 32768, 1								; for kdistribution
;giFile				ftgen	0, 0, 0, 1, "fox.wav", 0, 0, 0							; original soundfile reading for source waveform
giFile				init    100   													; table set by Unity     
giWin				ftgen	0, 0, 4096, 20, 9, 1									; grain envelope
;giPan				ftgen	0, 0, 32768, -21, 1		    							; for panning (random values between 0 and 1)


; *************************************************
; partikkel example, processing of soundfile
; uses the file set by Unity in table 100 											; the original example is using "fox.wav" 
; *************************************************

instr 1

/*score parameters*/
kgain           chnget "gain"                                                   ; the gain of a grain
kspeed	        chnget "speed"          			;= p4						; 1 = original speed 
kgrainrate	    chnget "density"        			;= p5						; grain rate
kgrainsize	    chnget "grainSize"      			;= p6						; grain size in ms
kcent		    chnget "grainPitch"     			;= p7						; transposition in cent
kposrand	   	chnget "jitter"	        			;= p8						; time position randomness (offset) of the pointer in ms
kcentrand	   	chnget "randPitch"      			;= p9						; transposition randomness in cents
kpan		    chnget "spread"	        			;= p10						; panning narrow (0) to wide (1)
kdist		    chnget "distribution"				;= p11						; grain distribution (0=periodic, 1=scattered)
kpandur         chnget "panDur"                                                 ; the duration (s) of the grain panning from output to output

/*get length of source wave file, needed for both transposition and time pointer*/
ifilen          = tableng(giFile)
ifildur			= ifilen / sr

/*sync input (disabled)*/
async			= 0		

/*grain envelope*/
kenv2amt		= 1		        ; use only secondary envelope
ienv2tab 		= giWin		    ; grain (secondary) envelope
ienv_attack		= -1 		    ; default attack envelope (flat)
ienv_decay		= -1 		    ; default decay envelope (flat)
ksustain_amount	= 0.5		    ; no meaning in this case (use only secondary envelope, ienv2tab)
ka_d_ratio		= 0.5 		    ; no meaning in this case (use only secondary envelope, ienv2tab)

/*amplitude*/
kamp			= kgain * 0dbfs	; grain amplitude
igainmasks		= -1		    ; (default) no gain masking

/*transposition*/
kcentrand		rand kcentrand	; random transposition
iorig			= 1 / ifildur	; original pitch
kwavfreq		= iorig * cent(kcent + kcentrand)

/*other pitch related (disabled)*/
ksweepshape		= 0		        ; no frequency sweep
iwavfreqstarttab= -1            ; default frequency sweep start
iwavfreqendtab	= -1            ; default frequency sweep end
awavfm			= 0		        ; no FM input
ifmamptab		= -1		    ; default FM scaling (=1)
kfmenv			= -1		    ; default FM envelope (flat)

/*trainlet related (disabled)*/
icosine			= giCosine	    ; cosine ftable
kTrainCps		= kgrainrate	; set trainlet cps equal to grain rate for single-cycle trainlet in each grain
knumpartials	= 1		        ; number of partials in trainlet
kchroma			= 1		        ; balance of partials in trainlet

/*panning, using channel masks*/
; channel masking table, using just one single mask here, 
; this portion is grabbed from partikkel-panlaws.csd
ichannelmasks	ftgentmp	0, 0, 32, -2,  0, 0,   0
; continuously write to masking table, 
; slowly panning the grains from output to output 
; over a kpandur second period
kchn phasor 0.1 ;1 / kpandur
kchn = kchn * 8
tablew kchn, 2, ichannelmasks

/*random gain masking (disabled)*/
krandommask		= 0	

/*source waveforms*/
kwaveform1		= giFile	    ; source waveform
kwaveform2		= giFile	    ; all 4 sources are the same
kwaveform3		= giFile
kwaveform4		= giFile
iwaveamptab		= -1		    ; (default) equal mix of source waveforms and no amplitude for trainlets

/*time pointer*/
afilposphas		phasor kspeed / ifildur
/*generate random deviation of the time pointer*/
kposrandsec		= kposrand / 1000	; ms -> sec
kposrand		= kposrandsec / ifildur	; phase values (0-1)
krndpos			linrand	 kposrand	; random offset in phase values
/*add random deviation to the time pointer*/
asamplepos1		= afilposphas + krndpos; resulting phase values (0-1)
asamplepos2		= asamplepos1
asamplepos3		= asamplepos1	
asamplepos4		= asamplepos1	

/*setting a sample pos control channel to read the position of the playbar*/
ksamplepos 		= k(afilposphas)
chnset ksamplepos, "samplepos"
;printks "samplepos: %f", 0.1, ksamplepos

/*original key for each source waveform*/
kwavekey1		= 1
kwavekey2		= kwavekey1	
kwavekey3		= kwavekey1
kwavekey4		= kwavekey1

/* maximum number of grains per k-period*/
imax_grains		= 100		

iopcode_id = 0 /* default, no connection to any partikkelsync instances */

a1, a2, a3, a4, a5, a6, a7, a8	partikkel kgrainrate, kdist, giDisttab, async, kenv2amt, ienv2tab, \
		ienv_attack, ienv_decay, ksustain_amount, ka_d_ratio, kgrainsize, kamp, igainmasks, \
		kwavfreq, ksweepshape, iwavfreqstarttab, iwavfreqendtab, awavfm, \
		ifmamptab, kfmenv, icosine, kTrainCps, knumpartials, \
		kchroma, ichannelmasks, krandommask, kwaveform1, kwaveform2, kwaveform3, kwaveform4, \
		iwaveamptab, asamplepos1, asamplepos2, asamplepos3, asamplepos4, \
		kwavekey1, kwavekey2, kwavekey3, kwavekey4, imax_grains, iopcode_id

chnset a1, "a1"
chnset a2, "a2"
chnset a3, "a3"
chnset a4, "a4"
chnset a5, "a5"
chnset a6, "a6"
chnset a7, "a7"
chnset a8, "a8"

chnset vuMeter(a1), "a1Vol"
chnset vuMeter(a2), "a2Vol"
chnset vuMeter(a3), "a3Vol"
chnset vuMeter(a4), "a4Vol"
chnset vuMeter(a5), "a5Vol"
chnset vuMeter(a6), "a6Vol"
chnset vuMeter(a7), "a7Vol"
chnset vuMeter(a8), "a8Vol"

endin

</CsInstruments>
<CsScore>
;i1		st	dur		speed	grate	gsize	cent	posrnd	cntrnd	pan		dist
; wait a little before starting, to let Unity load the samples and fill the table #100
i1		.5	z	
;i1 	0	2.757	1		200		15		0		0		0		0		0
;s
;i1		0	2.757	1		200		15		400		0		0		0		0
;s
;i1		0	2.757	1		15		450		400		0		0		0		0
;s
;i1		0	2.757	1		15		450		400		0		0		0		0.4
;s
;i1		0	2.757	1		200		15		0		400		0		0		1
;s
;i1		0	5.514	.5		200		20		0		0		600		.5		1
;s
;i1		0	11.028	.25		200		15		0		1000	400		1		1

</CsScore>
</CsoundSynthesizer>
