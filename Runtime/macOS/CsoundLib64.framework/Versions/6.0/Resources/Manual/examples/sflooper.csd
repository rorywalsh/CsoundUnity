<CsoundSynthesizer>
<CsOptions>
; Select audio/midi flags here according to platform
; Audio out   Audio in    No messages
-odac -+rtmidi=virtual  -M0     ;;;realtime audio in, midi in
; For Non-realtime ouput leave only the line below:
; -o sflooper.wav -W ;;; for file output any platform
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs  = 1


isf   sfload "07AcousticGuitar.sf2"
      sfpassign 0, isf

instr 1	; play from score and midi keyboard

      mididefault   60, p3
      midinoteonkey p4, p5
inum  init p4
ivel  init p5
print ivel

ivel  init    ivel/127		;velocity dependent
kamp  linsegr 1,1,1,.1,0	;envelope
kamp  = kamp * .0002		;scale amplitude (= kamp/5000)
kfreq init 1			;do not change freq from sf
;"07AcousticGuitar.sf2" contains 2 samples, on notes E1 and C#4
;start loop from beginning, loop .2 seconds - on the root key of these samples
aL,aR sflooper ivel, inum, kamp*ivel, kfreq, 0, 0, .2, .05
      outs aL, aR
	
endin
</CsInstruments>
<CsScore>
f0  60		; stay active for 1 minute

i1 0 1 60 100
i1 + 1 62 <
i1 + 1 65 <
i1 + 1 69 10
e
</CsScore>
</CsoundSynthesizer>
