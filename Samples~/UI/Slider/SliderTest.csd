<Cabbage>
form caption("Slider Test"), size(300, 100)

hslider bounds(0, 0, 300, 50) range(20, 16000, 440, .5, 100) valuetextbox(1) text("Frequency") channel("freqSlider") value(440) velocity(50)
</Cabbage>

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments> 
sr 	= 	48000 
ksmps 	= 	64
nchnls 	= 	2
0dbfs	=	1 

instr 1
a1 oscil .2, chnget:k("freqSlider"), 1
outs a1, a1

endin

</CsInstruments>
<CsScore>
f0 3600
f1 0 4096 10 1
i1 0 z
</CsScore>
</CsoundSynthesizer>
