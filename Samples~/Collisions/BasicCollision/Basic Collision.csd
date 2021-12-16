<Cabbage>
form caption("Basic Collision") size(600, 300)
rslider bounds(0, 0, 100, 100), channel("modIndex"), range(0, 6, 1.0), text("MOD Index")
rslider bounds(100, 0, 100, 100), channel("modFreq"), range(0, 100, 6.0), text("MOD Freq")

</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
sr = 48000
ksmps = 64
nchnls = 2
0dbfs = 1

instr 1

kenv linseg 0, 0.1, .05
kmodIndex chnget "modIndex"
kmodFreq chnget "modFreq"

kmodIndex lineto kmodIndex, 0.01
kmodFreq lineto kmodFreq, 0.01

amodosc oscili (kmodIndex + kmodFreq), kmodFreq, 1 
acarosc oscili kenv, 110 + amodosc, 1

aEnv      linenr    acarosc, 0, .01, .01 ; avoiding clicks at the note-end
 
outs aEnv, aEnv

endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
f1 0 [4096^2] 10 .1 0.3 0.1 0.1
;starts instruments and runs them for 7000 years too!
;i1 0.1 z 
</CsScore>
</CsoundSynthesizer>
