<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc for RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSbufread.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" and  "fox.ats" are created by atsa

ktime	line	0, p3, 4
ktime2	line	0, p3, 4
kline	expseg	0.001, .3, 1, p3-.3, 1
kline2	expseg	0.001, p3, 3
  	ATSbufread ktime2, 1, "fox.ats", 20
aout	ATScross   ktime, 2, "beats.ats", 1, kline, 0.001 * (4 - kline2), 180
	outs aout*2, aout*2

endin

</CsInstruments>
<CsScore>
; sine wave.
f 1 0 16384 10 1

i 1 0 4 
e
</CsScore>
</CsoundSynthesizer>
