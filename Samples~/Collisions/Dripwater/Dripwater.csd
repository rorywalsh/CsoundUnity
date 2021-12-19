<Cabbage>
form caption("DripWater") size(800, 800)
rslider bounds(0, 100, 100, 100), channel("amp"), range(0, 0.02, 0.0005), text("Amp")
rslider bounds(100, 100, 100, 100), channel("dropSize"), range(1, 30, 21.5), text("Drop Size")
rslider bounds(200, 100, 100, 100), channel("reverbTime"), range(0, 0.99, 0.87), text("Reverb Time")
rslider bounds(300, 100, 100, 100), channel("reverbFilter"), range(0, 16000, 14500), text("Reverb Filter")
button bounds (0, 200, 100, 100), channel("drop"), range(0, 1, 0), text("Push to Drop")
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

gadripwater init 0

; MAIN INSTRUMENT
instr 1

kTrigger chnget "drop"
    
if changed(kTrigger) == 1 then
    event "i", 2, 0, 1, 420, 650, 1000
endif
    
kRevTime chnget "reverbTime" ;= 0.85
kRevFilt chnget "reverbFilter"
denorm gadripwater  
aRevL, aRevR reverbsc gadripwater, gadripwater, kRevTime, kRevFilt, sr, 0, 1

clear	gadripwater

outs aRevL, aRevR

endin

;DRIPWATER INSTRUMENT
instr	2	

iamp chnget "amp" ;= .0111
idropSize chnget "dropSize"

gkdettack = 0.15
gknum chnget "dropSize"; = 10
gkdamp = 1.75
gkmaxshake = 0.001
ifreq = p4 
ifreq1 = p5 
ifreq2 = p6 

;printks "p4: %f p5: %f p6: %f\n", 0.1, p4, p5, p6
gadripwater 	dripwater 	iamp, i(gkdettack) , i(gknum), i(gkdamp) , i(gkmaxshake), ifreq , ifreq1, ifreq2
gadripwater clip gadripwater, 2, 0.1

endin


</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
;start main instrument and runs it for 7000 years too!
i1 0.0 z 
</CsScore>
</CsoundSynthesizer>
