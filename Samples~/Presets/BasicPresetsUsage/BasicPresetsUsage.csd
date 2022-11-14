<Cabbage>
form caption("BasicPresetsUsage") size(400, 350), colour(58, 110, 182)
rslider bounds(288, 192, 100, 100), channel("gain"), range(0, 1, 0.4, 1, 0.01), text("Gain")
hslider bounds(10, 70, 270, 20), channel("freq"), range(10, 2200, 440, 1, 0.001), text("Carrier Frequency")
hslider bounds(10, 90, 270, 20), channel("modFreq"), range(0, 200, 10), text("Mod Frequency")
rslider bounds(10, 10, 60, 60) range(0, 1, 1, 1, 0.001), channel("hrm1"), text("Base Frequency Amp")
rslider bounds(80, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm2"), text("First Harmonic Amp")
rslider bounds(150, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm3"), text("Second Harmonic Amp")
rslider bounds(220, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm4"), text("Third Harmonic Amp")
combobox bounds(290, 14, 100, 30), channel("carWaveform"), text("sine", "saw", "square", "triangle","impulse","noise")
combobox bounds(290, 54, 100, 30), channel("modWaveform"), text("sine", "saw", "square", "triangle","impulse","noise")
rslider bounds(310, 90, 60, 60) channel("modIndex") range(0, 15, 0, 1, 0.001), text("Mod Index")
groupbox bounds(10, 114, 270, 220) channel("filterGroupBox") text("FILTER")
checkbox bounds(20, 140, 125, 30) channel("filtOn") text("Filter ON")
rslider bounds(14, 180, 65, 65) channel("filtFreq") range(0, 16000, 0, 1, 0.001) text("F Freq")
rslider bounds(14, 256, 65, 65) channel("filtRandFreq") range(0, 16000, 0, 1, 0.001) text("F Rand Freq")
rslider bounds(80, 180, 65, 65) channel("filtRes") range(0, 1.99, 0, 1, 0.001) text("F Res")
rslider bounds(80, 256, 65, 65) channel("filtRandRes") range(0, 1.99, 0, 1, 0.001) text("F Rand Res")
checkbox bounds(150, 140, 120, 30) channel("filtLFOOn") text("Filter LFO ON")
combobox bounds(150, 176, 119, 30), channel("filtLFOShape"), text("sine", "triangles", "square bi", "square uni", "saw", "saw down") 
rslider bounds(150, 270, 60, 60) channel("filtLFOAmp") range(0, 0.99, 0, 1, 0.001), text("LFO Amp")
rslider bounds(150, 210, 60, 60) channel("filtLFOFreq") range(0, 30, 0, 1, 0.001) text("LFO Freq") 
rslider bounds(210, 270, 60, 60) channel("filtLFORandAmp") range(0, 1, 0, 1, 0.001) text("LFO Rand Amp")
rslider bounds(210, 210, 60, 60) channel("filtLFORandFreq") range(0, 30, 0, 1, 0.001) text("LFO Rand Freq")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Initialize the global variables. 
sr = 48000
ksmps = 32
nchnls = 2
0dbfs = 1

giSine    ftgen     1, 0, 2^10, 10, 1
giSaw     ftgen     2, 0, 2^10, 10, 1,-1/2,1/3,-1/4,1/5,-1/6,1/7,-1/8,1/9
giSquare  ftgen     3, 0, 2^10, 10, 1, 0, 1/3, 0, 1/5, 0, 1/7, 0, 1/9
giTri     ftgen     4, 0, 2^10, 10, 1, 0, -1/9, 0, 1/25, 0, -1/49, 0, 1/81
giImp     ftgen     5, 0, 2^10, 10, 1, 1, 1, 1, 1, 1, 1, 1, 1
giNoise   ftgen     6, 0, 2^10, 21, 1

;giWave1 ftgen 1, 0, 4096, 10, 1
;giWave2 ftgen 2, 0, 4096, 10, 1, .5, .25, .17
;giWave3 ftgen 3, 0, 4096, 10, 1, 0, .333, 0, .2, 0, .143, 0, .111, 0, .0909, 0, .077, 0, .0666, 0, .0588
;giNoise ftgen 4, 0, 4096, 21, 1

instr 1

    kEnv madsr 0.1, .5, .6, 1
        
    kFiltRandFreq = chnget:k("filtRandFreq")
    kFiltRandRes = chnget:k("filtRandRes")
    kLFORandAmp = chnget:k("filtLFORandAmp")
    kLFORandFreq = chnget:k("filtLFORandFreq")
    
    kFiltRandFreq random -kFiltRandFreq, kFiltRandFreq
    kFiltRandRes random -kFiltRandRes, kFiltRandRes
    kLFORandAmp random -kLFORandAmp, kLFORandAmp
    kLFORandFreq random -kLFORandFreq, kLFORandFreq
        
    aModOsc oscilikt chnget:k("modIndex") * chnget:k("modFreq"), chnget:k("modFreq"), chnget:k("modWaveform")
    
    kShapeChanged changed chnget:k("filtLFOShape")
    if kShapeChanged == 1 then
        reinit lfoShapeInit
    endif
    
    lfoShapeInit:
    afiltLFO lfo chnget:k("filtLFOAmp") + kLFORandAmp, chnget:k("filtLFOFreq") + kLFORandFreq, chnget:i("filtLFOShape")-1
    rireturn
    
    a1  oscilikt kEnv*chnget:k("hrm1"), chnget:k("freq")   + aModOsc, chnget:k("carWaveform")
    a2  oscilikt kEnv*chnget:k("hrm2"), chnget:k("freq")*2 + aModOsc, chnget:k("carWaveform")
    a3  oscilikt kEnv*chnget:k("hrm3"), chnget:k("freq")*3 + aModOsc, chnget:k("carWaveform")
    a4  oscilikt kEnv*chnget:k("hrm4"), chnget:k("freq")*4 + aModOsc, chnget:k("carWaveform")
    aMix = a1+a2+a3+a4
    
    kFiltOn = chnget:k("filtOn")
    kLFOOn = chnget:k("filtLFOOn")
    
    if kFiltOn == 1 then   
        if kLFOOn == 1 then
            aOut moogladder aMix, (chnget:k("filtFreq") + abs(kFiltRandFreq)) * abs(afiltLFO), chnget:k("filtRes") + abs(kFiltRandRes)
        else
            aOut moogladder aMix, chnget:k("filtFreq") + abs(kFiltRandFreq), chnget:k("filtRes") + abs(kFiltRandRes)
        endif
    else
        aOut = aMix
    endif
    
    outs aOut*kEnv*chnget:k("gain"), aOut*kEnv*chnget:k("gain")
    
endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
i1 0 z
</CsScore>
</CsoundSynthesizer>
