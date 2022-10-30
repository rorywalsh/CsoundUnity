<Cabbage> bounds(0, 0, 0, 0)
form caption("Process Clip") size(400, 300)
rslider bounds(0, 0, 77, 87) channel("gain") range(0, 2, 1, 1, 0.001)  valueTextBox(1) text("gain") 
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d 
</CsOptions>
<CsInstruments>
sr = 44100
ksmps = 10
nchnls = 2
nchnls_i = 2

0dbfs = 1

instr 1
ainL, ainR ins
kGain chnget "gain"
outs ainL*kGain, ainR*kGain
endin
</CsInstruments>
<CsScore>
f0 z
i1 0 [24*60*60]
</CsScore>
</CsoundSynthesizer>
