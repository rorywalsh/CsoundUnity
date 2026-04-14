<Cabbage>
form caption("Template") size(310, 100), guiMode("queue"), pluginId("def1")
nslider bounds(12, 10, 57, 22) channel("freqSlider") text("Midi Note") range(38, 88, 72, 1, 1) velocity(50)
label bounds(12, 40, 100, 22) channel("freqLabel") text("Midi Note")


</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Initialize the global variables.
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1
kEnv adsr .3, .2, .5, .4

aOut vco2 kEnv, cpsmidinn:k(chnget:k("freqSlider"))
outs aOut, aOut
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i1 0 z
</CsScore>
</CsoundSynthesizer>
