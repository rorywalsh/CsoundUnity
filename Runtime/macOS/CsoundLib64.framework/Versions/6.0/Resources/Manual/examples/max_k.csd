<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac  -Ma   ;;;realtime audio out and midi in (on all inputs)
;-iadc    ;;;uncomment -iadc if RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o max_k.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1


	FLpanel	"This Panel contains VU-meter",300,100
gk1,gih1 FLslider "VU-meter", 0,1,0,1, -1, 250,30, 30,30
	FLsetColor2 50, 50, 255,  gih1
	FLpanel_end
	FLrun

ga1 init 0
	
instr 1

kenv	linsegr	0,.5,.7,.5,.5,.2,0
ifreq	cpsmidi
a1	poscil	0dbfs*kenv, ifreq, 1
ga1	=	ga1+a1

endin

instr 2

	outs	ga1, ga1
ktrig	metro	25					;refresh 25 times per second
kval	max_k	ga1, ktrig, 1
	FLsetVal ktrig, kval, gih1
ga1	=	0

endin

</CsInstruments>
<CsScore>
f1 0 1024 10 1

i2 0 3600
f0 3600

e
</CsScore>
</CsoundSynthesizer>

