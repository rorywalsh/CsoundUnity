<Cabbage>
form caption("Chord Sample Timeline") size(570, 220), guiMode("queue"), pluginId("chtl")
rslider bounds(10,  10, 80, 80) channel("gain")      range(0, 1, 0.7, 1, 0.001)         text("Gain")
rslider bounds(100, 10, 80, 80) channel("amp")       range(0, 1, 0.5, 1, 0.001)         text("Amp")
rslider bounds(190, 10, 80, 80) channel("attack")    range(0.001, 0.5, 0.015, 1, 0.001) text("Attack")
rslider bounds(280, 10, 80, 80) channel("release")   range(0.001, 1.0, 0.25,  1, 0.001) text("Release")
rslider bounds(370, 10, 80, 80) channel("cutoff")    range(200, 12000, 5000,  1, 1)     text("Cutoff")
rslider bounds(460, 10, 80, 80) channel("reverbMix") range(0, 1, 0.35, 1, 0.001)        text("Reverb")
rslider bounds(10, 110, 80, 80) channel("resonance") range(0, 1, 0.2, 1, 0.001)         text("Resonance")
rslider bounds(100,110, 80, 80) channel("fmRatio")   range(0.5, 8.0, 1.0, 1, 0.01)     text("FM Ratio")
rslider bounds(190,110, 80, 80) channel("fmAmount")  range(0, 1, 0.0, 1, 0.001)         text("FM Amt")
rslider bounds(280,110, 80, 80) channel("panSpread") range(0, 1, 0.5, 1, 0.01)          text("Pan Spread")
rslider bounds(370,110, 80, 80) channel("detune")    range(0, 10, 0, 1, 0.01)           text("Detune (ct)")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Chord Sample Timeline — polyphonic instrument tuned for chord voicings
; p4 = frequency in Hz

sr     = 48000
ksmps  = 32
nchnls = 2
0dbfs  = 1

giSaw  ftgen 1, 0, 4096, 10, 1, 0.5, 0.333, 0.25, 0.2, 0.166, 0.142, 0.125
giSine ftgen 2, 0, 4096, 10, 1
ga1    init  0

instr 1
    iFreq    = p4
    iDur     = p3

    iGain    = chnget:i("gain")
    iAmp     = chnget:i("amp")
    iAtk     = chnget:i("attack")
    iRel     = chnget:i("release")
    iFmRatio = chnget:i("fmRatio")
    iFmAmt   = chnget:i("fmAmount")
    iSpread  = chnget:i("panSpread")
    iDetune  = chnget:i("detune")

    kCutoff  chnget "cutoff"
    kQ       chnget "resonance"

    xtratim iRel * 2

    iAtk    = min(iAtk, iDur * 0.5)
    iRel    = min(iRel, iDur * 0.5)
    iSusDur = max(iDur - iAtk - iRel, 0)
    aEnv    linseg 0, iAtk, iAmp, iSusDur, iAmp, iRel, 0, iRel * 2, 0

    iModFreq = iFreq * iFmRatio
    aModAmt  = aEnv * iFmAmt * iFreq * 2
    aMod     oscili aModAmt, iModFreq, giSine

    iFreq2  = iFreq * semitone(iDetune * 0.01)
    aOsc1   oscili aEnv, iFreq  + aMod, giSaw
    aOsc2   oscili aEnv, iFreq2 + aMod, giSaw
    aMix    = (aOsc1 + aOsc2) * 0.5

    aFilt   moogvcf2 aMix, kCutoff, kQ * 0.8

    iPan    random 0.5 - iSpread * 0.5, 0.5 + iSpread * 0.5
    aL, aR  pan2 aFilt * iGain, iPan
    outs aL, aR
    ga1 += (aL + aR) * 0.5 * iGain
endin

instr 99
    kMix chnget "reverbMix"
    aWetL, aWetR reverbsc ga1, ga1, 0.85, 8000
    outs aWetL * kMix, aWetR * kMix
    ga1 = 0
endin

</CsInstruments>
<CsScore>
i99 0 [60*60*24*7]
f0 z
</CsScore>
</CsoundSynthesizer>
