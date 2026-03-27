<Cabbage>
form caption("TestButton") size(150, 100), guiMode("queue"), pluginId("btn1")
button bounds(10, 10, 80, 40) channel("trigger") text("Push Me")
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

;instrument will be triggered by score sent by Unity UI button
instr 1
kEnv madsr .1, .2, .6, .4
aOut vco2 0.5, 440
outs aOut*kEnv, aOut*kEnv
endin

instr 2
kTrig init 0
kTrig = chnget:k("trigger")
kChanged changed kTrig

printk2 kTrig

if (kTrig == 1 && kChanged == 1) then
   kTrig = 0
   chnset(kTrig, "trigger")
   schedulek 1, 0, 3
endif

endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i2 0 z
</CsScore>
</CsoundSynthesizer>
