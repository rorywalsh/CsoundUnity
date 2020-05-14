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


instr 1
    prints "P4" 
    iWaves[] fillarray 0, 2, 4
    print p4
    kEnv madsr 0.1, .5, .6, 1
    print chnget:i("waveform")
    a1 vco2 kEnv*chnget:k("hrm1"), cpsmidinn(p4), iWaves[chnget:i("waveform")-1], .5
    a2 vco2 kEnv*chnget:k("hrm2"), cpsmidinn(p4)*2, iWaves[chnget:i("waveform")-1], .5
    a3 vco2 kEnv*chnget:k("hrm3"), cpsmidinn(p4)*3, iWaves[chnget:i("waveform")-1], .5
    a4 vco2 kEnv*chnget:k("hrm4"), cpsmidinn(p4)*4, iWaves[chnget:i("waveform")-1], .5
    aMix = a1+a2+a3+a4
    outs aMix*kEnv*chnget:k("gain"), aMix*kEnv*chnget:k("gain")
endin

instr 2
a1 oscili 1, 440
chnset a1, "oscil"
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i2 0 z
;starts instrument 1 and runs it for a week
</CsScore>
</CsoundSynthesizer>
