<CsoundSynthesizer>
<CsOptions>
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs = 1

instr 1
    kFrequency chnget "frequency"
    kFrequency portk max(kFrequency, 20), 0.02
    aSource vco2 0.15, kFrequency
    fAnalysis pvsanal aSource, 1024, 256, 1024, 1
    pvsout fAnalysis, "analysis"

    fProcessed pvsin "processed", 1024, 256, 1024, 1, 0
    aProcessed pvsynth fProcessed
    kMix chnget "processedMix"
    kMix portk kMix, 0.02
    kListen chnget "listen"
    kListen portk kListen, 0.02
    aOutput = (aSource * (1 - kMix) + aProcessed * kMix) * kListen
    outs aOutput, aOutput
endin
</CsInstruments>
<CsScore>
f0 z
; Unity initializes the input PVS channel before starting instrument 1.
</CsScore>
</CsoundSynthesizer>
