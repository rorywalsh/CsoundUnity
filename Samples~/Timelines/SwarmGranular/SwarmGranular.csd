<Cabbage>
form caption("Swarm Granular") size(570, 200), guiMode("queue"), pluginId("swgr")
rslider bounds(10,  10, 80, 80) channel("gain")      range(0, 1, 0.7, 1, 0.001)   text("Master Gain")
rslider bounds(100, 10, 80, 80) channel("grainAmp")  range(0, 1, 0.1, 1, 0.001)   text("Grain Amp")
rslider bounds(190, 10, 80, 80) channel("attack")    range(0.001, 0.5, 0.01, 1, 0.001) text("Attack")
rslider bounds(280, 10, 80, 80) channel("release")   range(0.001, 0.5, 0.05, 1, 0.001) text("Release")
rslider bounds(370, 10, 80, 80) channel("panSpread") range(0, 1, 0.5, 1, 0.01)    text("Pan Spread")
rslider bounds(460, 10, 80, 80) channel("sustain")   range(0, 1, 1, 1, 0.001)     text("Sustain Amp")
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
; Swarm Granular - each note event is a single grain
; p4 = frequency in Hz (set by CsoundUnity Swarm mode with pitch randomization)
;
; Channels:
;   gain       - master output gain (0-1)
;   grainAmp   - per-grain amplitude peak (0-1, keep low with many overlapping grains)
;   attack     - grain attack time in seconds
;   release    - grain release time in seconds
;   sustain    - sustain level relative to grainAmp (0-1)
;   panSpread  - stereo spread of random pan (0 = center, 1 = full random)
;
; Envelope: attack ramps from 0 to grainAmp, sustains until (p3 - release), then releases to 0.
; Attack and release are clamped so their sum never exceeds p3.

sr     = 48000
ksmps  = 32
nchnls = 2
0dbfs  = 1

giSine ftgen 1, 0, 4096, 10, 1

instr 1
    iFreq   = p4                            ; frequency sent by Swarm mode
    iDur    = p3                            ; grain duration (note duration)
    iGain   = chnget:i("gain")
    iAmp    = chnget:i("grainAmp")          ; per-grain amplitude peak
    iAtk    = chnget:i("attack")
    iRel    = chnget:i("release")
    iSus    = chnget:i("sustain")           ; sustain level (fraction of iAmp)
    iSpread = chnget:i("panSpread")

    ; Clamp attack + release so they never exceed the grain duration
    iTotal  = iAtk + iRel
    iScale  = (iTotal > iDur ? iDur / iTotal : 1)
    iAtk    = iAtk * iScale
    iRel    = iRel * iScale

    ; Explicit envelope: 0 -> peak -> sustain level -> 0
    ; Sustain segment fills the time between attack end and release start
    iSusTime = iDur - iAtk - iRel
    iSusAmp  = iAmp * iSus
    aEnv    linseg 0, iAtk, iAmp, iSusTime, iSusAmp, iRel, 0

    ; Oscillator at grain frequency
    aGrain  oscili aEnv * iGain, iFreq, giSine

    ; Random stereo pan per grain
    iPan    random 0.5 - iSpread * 0.5, 0.5 + iSpread * 0.5
    aL, aR  pan2 aGrain, iPan

    outs aL, aR
endin

</CsInstruments>
<CsScore>
; Keep Csound running indefinitely - grains are triggered by score events from Unity
f0 z
</CsScore>
</CsoundSynthesizer>
