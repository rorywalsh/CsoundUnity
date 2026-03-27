<Cabbage>
form caption("Test Filling Bar") size(400, 300), guiMode("queue"), pluginId("fill")
hslider bounds(100, 124, 200, 50) channel("FillAmount") range(0, 1, 0, 1, 0.001) velocity(0.84) valueTextBox(1)
hslider bounds(100, 174, 200, 50) channel("MinFrequency") range(50, 2500, 440, 1, 0.001) valueTextBox(1)
hslider bounds(100, 224, 200, 50) channel("MaxFrequency") range(50, 2500, 880, 1, 0.001) valueTextBox(1)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d 
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
nchnls = 2
0dbfs = 1

;instrument will be triggered by keyboard widget
instr 1
;kEnv adsr .1, .2, .6, .4
kMinFreq = chnget:k("MinFrequency")
kMaxFreq = chnget:k("MaxFrequency")
kAmount = chnget:k("FillAmount")
kFreq init 0.0001
kFreq = kMinFreq + (kMaxFreq - kMinFreq) * kAmount
;printks "kAmount: %f - %f - %f\n", 0.1, kAmount, chnget:k("FillAmount"), chnget:k("MaxFrequency")
aEnv madsr 0.2, 0.1, 0.5, 0.1
aOut oscili aEnv, kFreq, 1
outs aOut, aOut
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
;i1 0 3600
f1 0 4096 10 1
</CsScore>
</CsoundSynthesizer>
