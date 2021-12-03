<Cabbage> bounds(0, 0, 0, 0)
form caption("Process Clip") size(400, 300)
rslider bounds(0, 0, 77, 87) channel("gain") range(0, 2, 1, 1, 0.001)  valueTextBox(1) text("gain") 
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d 
</CsOptions>
<CsInstruments>
sr = 48000
ksmps = 64
nchnls = 2
nchnls_i = 1
0dbfs = 1

instr 1
ain in 1
kGain chnget "gain"
outs ain*kGain, ain*kGain
endin
</CsInstruments>
<CsScore>
f0 z
i1 0 [24*60*60]
</CsScore>
</CsoundSynthesizer>
