<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
-odac     ;;;RT audio out
;-iadc    ;;;uncomment -iadc for RT audio input is needed too
; For Non-realtime ouput leave only the line below:
; -o ATSinterpread.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1	; "beats.ats" is created by atsa

ktime	line	0, p3, 1.8
	ATSbufread ktime, 1, "beats.ats", 42
kamp	ATSinterpread 	p4
aosc	oscili	kamp, p4, 1
	outs	aosc * 25, aosc *25

endin

</CsInstruments>
<CsScore>
; sine wave.
f 1 0 16384 10 1

i 1 0 2 100
e

</CsScore>
</CsoundSynthesizer>
