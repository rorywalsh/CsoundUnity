<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac  -+rtmidi=virtual -M0   ;;;realtime audio out and realtime midi in
;-iadc    ;;;uncomment -iadc if realtime audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o mxadsr.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1

instr 1
 	 
icps	cpsmidi	 	 
iamp	ampmidi	.5	 
 	 	 	 
kenv	mxadsr	0.5, 0, 1, 0.5
asig	pluck	kenv, icps, icps, 2, 1	 
	outs	asig, asig
	 
endin
</CsInstruments>
<CsScore>
f 2 0 4096 10 1	

f0 30	;runs 30 seconds
e
</CsScore>
</CsoundSynthesizer>