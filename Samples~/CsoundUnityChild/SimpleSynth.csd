<Cabbage>
form caption("Untitled") size(400, 300), colour(58, 110, 182), pluginid("def1")
rslider bounds(296, 162, 100, 100), channel("gain"), range(0, 1, .4, 1, .01), text("Gain"), trackercolour("lime"), outlinecolour(0, 0, 0, 50), textcolour("black")

rslider bounds(10, 10, 60, 60) range(0, 1, 1, 1, 0.001), channel("hrm1")
rslider bounds(80, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm2")
rslider bounds(150, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm3")
rslider bounds(220, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm4")

keyboard bounds(10, 82, 316, 70)
combobox bounds(290, 14, 100, 30), channel("waveform"), text("W1", "W2", "W3")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d -+rtmidi=NULL -M0 --midi-key=4 --midi-velocity-amp=5
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
ksmps = 32
nchnls = 2
0dbfs = 1

giWave1 ftgen 1, 0, 4096, 10, 1
giWave2 ftgen 1, 0, 4096, 10, 1, .5, .25, .17

;this instrument sends audio to two named channels
;this audio can be picked up by any CsoundUnityNode component..
instr ChildSounds
    a1 oscili 1, 440, giWave1
    chnset a1, "sound1"
    a2 oscili 1, 840, giWave1
    chnset a2, "sound2"
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i"ChildSounds" 0 z
;starts instrument 1 and runs it for a week
</CsScore>
</CsoundSynthesizer>
