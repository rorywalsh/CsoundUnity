<Cabbage>
form caption("BasicPresetsUsage") size(400, 300), colour(58, 110, 182)
rslider bounds(288, 174, 100, 100), channel("gain"), range(0, 1, 0.4, 1, 0.01), text("Gain")
hslider bounds(10, 70, 270, 20), channel("freq"), range(10, 2200, 440, 1, 0.001), text("Carrier Frequency")
hslider bounds(10, 90, 270, 20), channel("modFreq"), range(0, 200, 10), text("Mod Frequency")
rslider bounds(10, 10, 60, 60) range(0, 1, 1, 1, 0.001), channel("hrm1")
rslider bounds(80, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm2")
rslider bounds(150, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm3")
rslider bounds(220, 10, 60, 60) range(0, 1, 0, 1, 0.001), channel("hrm4")
combobox bounds(290, 14, 100, 30), channel("carWaveform"), text("car W1", "car W2", "car W3")
combobox bounds(290, 54, 100, 30), channel("modWaveform"), text("mod W1", "mod W2", "mod W3")
rslider bounds(310, 90, 60, 60) channel("modIndex") range(0, 1, 0, 1, 0.001), text("mod Index")
rslider bounds(150, 130, 60, 60) channel("lfoAmp") range(0, 0.99, 0, 1, 0.001) text("LFO Amp")
rslider bounds(220, 130, 60, 60) channel("lfoFreq") range(0, 30, 0, 1, 0.001) text("LFO Freq")
rslider bounds(150, 200, 60, 60) channel("lfoAmpRand") range(0, 1, 0, 1, 0.001) text("LFO Amp Rand")
rslider bounds(220, 200, 60, 60) channel("lfoFreqRand") range(0, 30, 0, 1, 0.001) text("LFO Freq Rand")

checkbox bounds(16, 160, 100, 26) channel("filtOn") text("filter on")
rslider bounds(10, 200, 60, 60) channel("filtFreq") range(0, 16000, 0, 1, 0.001) text("Filter Freq")
rslider bounds(70, 200, 60, 60) channel("filtRes") range(0, 1.99, 0, 1, 0.001) text("Filter Res")

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

giWave1 ftgen 1, 0, 4096, 10, 1
giWave2 ftgen 2, 0, 4096, 10, 1, .5, .25, .17
giWave3 ftgen 3, 0, 4096, 10, 1, 0, .333, 0, .2, 0, .143, 0, .111, 0, .0909, 0, .077, 0, .0666, 0, .0588

instr 1

    kEnv madsr 0.1, .5, .6, 1
    
    kLFOAmpRand = chnget:k("lfoAmpRand")
    kLFOFreqRand = chnget:k("lfoFreqRand")
    
    kLFOAmpRand1 random -kLFOAmpRand, kLFOAmpRand
    kLFOAmpRand2 random -kLFOAmpRand, kLFOAmpRand
    kLFOAmpRand3 random -kLFOAmpRand, kLFOAmpRand
    kLFOAmpRand4 random -kLFOAmpRand, kLFOAmpRand
    kLFOFreqRand1 random -kLFOFreqRand, kLFOFreqRand
    kLFOFreqRand2 random -kLFOFreqRand, kLFOFreqRand
    kLFOFreqRand3 random -kLFOFreqRand, kLFOFreqRand
    kLFOFreqRand4 random -kLFOFreqRand, kLFOFreqRand
    
    aModOsc oscilikt chnget:k("modIndex") * chnget:k("modFreq"), chnget:k("modFreq"), chnget:k("modWaveform")
    
    aLFO1 lfo chnget:k("lfoAmp") + kLFOAmpRand1, chnget:k("lfoFreq") + kLFOFreqRand1, 0
    aLFO2 lfo chnget:k("lfoAmp") + kLFOAmpRand2, chnget:k("lfoFreq") + kLFOFreqRand2, 0
    aLFO3 lfo chnget:k("lfoAmp") + kLFOAmpRand3, chnget:k("lfoFreq") + kLFOFreqRand3, 0
    aLFO4 lfo chnget:k("lfoAmp") + kLFOAmpRand4, chnget:k("lfoFreq") + kLFOFreqRand4, 0

    a1  oscilikt kEnv*chnget:k("hrm1"), chnget:k("freq")   + aLFO1 + aModOsc, chnget:k("carWaveform")
    a2  oscilikt kEnv*chnget:k("hrm2"), chnget:k("freq")*2 + aLFO2 + aModOsc, chnget:k("carWaveform")
    a3  oscilikt kEnv*chnget:k("hrm3"), chnget:k("freq")*3 + aLFO3 + aModOsc, chnget:k("carWaveform")
    a4  oscilikt kEnv*chnget:k("hrm4"), chnget:k("freq")*4 + aLFO4 + aModOsc, chnget:k("carWaveform")
    aMix = a1+a2+a3+a4
    
    kFiltOn = chnget("filtOn")
    ;kFiltOn cabbageGetValue "filtOn"
    
    ;printk 0.1, kFiltOn 
    
    if kFiltOn == 1 then   
        aOut moogladder aMix, chnget:k("filtFreq"), chnget:k("filtRes")
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
