<Cabbage>
form caption("Label Test") size(310, 100), guiMode("queue"), pluginId("def1")
hslider bounds(0, 20, 300, 50) range(20, 4000, 440, 1, 0.001) valuetextbox(1) channel("freqSlider") value(440) velocity(50)
label bounds(0, 60, 100, 30) channel("freqLabel") text("Frequency") fontSize(20)
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
; linsegr, not adsr: adsr works out its release from p3, and this note has no duration
; (i1 0 z), so it fails at init and Csound drops the note. linsegr holds until the release.
kEnv linsegr 0, .3, 1, .2, .5, .4, 0
aOut vco2 kEnv, chnget:k("freqSlider")
outs aOut, aOut
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i1 0 z
</CsScore>
</CsoundSynthesizer>
