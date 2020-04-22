<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in    No messages
-odac 	-d    -m0d     -M0  -+rtmidi=virtual ;;;RT audio I/O with MIDI in
; For Non-realtime ouput leave only the line below:
; -o midiin.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

massign 1, -1; prevent triggering of instrument with MIDI

instr 100
kMode = 3
kTempo = 6
kNote, kCounter midiarp kTempo

kFilterFreq oscil 2000, .05
;if kCounter is 1 trigger instrument 2 to play
if kCounter==1 then 	
	event "i", 200, 0, 2, kNote, kFilterFreq+2200
endif

endin

instr 200
kEnv expon .4, p3, .001
aOut vco2 kEnv, cpsmidinn(p4)*2		;convert note number to cps
aFilter moogladder aOut, p5, 0
outs aFilter, aFilter
endin

</CsInstruments>
<CsScore>
i100 0 1000
</CsScore>
</CsoundSynthesizer>
