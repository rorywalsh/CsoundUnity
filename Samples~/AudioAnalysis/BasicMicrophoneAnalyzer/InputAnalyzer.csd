<Cabbage>
form caption("Input Analyzer") size(400, 300)
rslider bounds(0, 0, 100, 100), channel("gain"), range(0, 10, 1.0, 1, .01), text("Gain")
rslider bounds(100, 0, 100, 100), channel("update"), range(0.001, 0.5, 0.01, 1, 0.001), text("Update")
rslider bounds(0, 100, 100, 100), channel("threshold"), range(0, 130, 40, 1, 0.0005), text("Threshold")
rslider bounds(100, 100, 100, 100), channel("minFreq"), range(0, 127, 30, 1, 0.0005), text("Min Freq")
rslider bounds(200, 100, 100, 100), channel("maxFreq"), range(0, 127, 80, 1, 0.0005), text("Max Freq")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
sr = 48000
ksmps = 64
ksmps = 32
nchnls = 2
nchnls_i = 1
0dbfs = 1

opcode vuMeter, k, a
ain xin
ametera1	follow 		ain, .01		;CREATE AN AMPLITUDE FOLLOWING UNIPOLAR SIGNAL (NEEDED FOR THE AMPLITUDE METERS)
kmetera1	downsamp	ametera1		;CONVERT AMPLITUDE FOLLOWING SIGNAL TO K-RATE
kmetera1	portk		kmetera1, .1	;SMOOTH THE MOVEMENT OF THE AMPLITUDE FOLLOWING SIGNAL - THIS WILL MAKE THE METERS EASIER TO VIEW
xout kmetera1
endop

instr 1

; get current gain
kGain chnget "gain"

; get input channel
a1 in
; scale the input with the gain
a1 *= kGain

;printks "a1: %d\n", 0.1, a1
iupdte chnget "update" ; init 0.01 ;

; these values can be tuned to achieve better tracking
; range in which pitch is detected, expressed in octave point decimal
ilo = octmidinn(chnget("minFreq"))
ihi = octmidinn(chnget("maxFreq"))
; amplitude, expressed in decibels, necessary for the pitch to be detected. Once started it continues until it is 6 dB down.
idbthresh = chnget("threshold")
; number of divisons of an octave. Default is 12 and is limited to 120.
ifrqs = 12
; the number of conformations needed for an octave jump. Default is 10.
iconf = 20
; starting pitch for tracker. Default value is (ilo + ihi)/2.
istrt = 7.5

;lowpass input to achieve better tracking
a1 tone a1, 11000

;pitch and amp estimation
koct, kamp pitch a1, iupdte, ilo, ihi, idbthresh, ifrqs, iconf, istrt

;also estimate the rms value 
krms rms a1

khertz = cpsoct(koct)
;printks "freq: %f %f\n", 0.1, khertz, koct 

chnset koct, "oct" 
chnset khertz, "hertz"
chnset kamp, "amp" 
chnset krms, "rms"

endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
;starts instrument 1 and runs it for 7000 years too!
i1 0.1 z 
</CsScore>
</CsoundSynthesizer>
