<Cabbage>
form caption("Range Slider Test") size(500, 230)

hrange bounds(10, 10, 440, 40) channel("freqLo", "freqHi") range(80, 8000, 300:2400, 0.35, 1) text("Filter range (Hz)")
hrange bounds(10, 70, 440, 40) channel("noteLo", "noteHi") range(36, 96, 48:72, 1, 1) text("Arp note range (MIDI)")
vrange bounds(450, 10, 50, 200) channel("rateLo", "rateHi") range(0.05, 0.30, 0.1:0.25, 1, 0.001) text("Random Rate")
hslider bounds(10, 130, 400, 40) channel("gain") range(0, 1, 0.6, 1, 0.01) text("Gain")

</Cabbage>

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

sr     = 48000
ksmps  = 64
nchnls = 2
0dbfs  = 1

; -----------------------------------------------------------------------
; instr 1 — filtered noise layer
;   freqLo / freqHi  → bandpass center and bandwidth
;   Moving either handle sweeps the tone colour in real time.
; -----------------------------------------------------------------------
instr 1
    kLo  chnget "freqLo"
    kHi  chnget "freqHi"
    kGain chnget "gain"
    

    ; Guard: ensure Hi > Lo to avoid zero/negative bandwidth
    kHi  = (kHi > kLo + 1 ? kHi : kLo + 1)

    kCenter = (kLo + kHi) * 0.5
    kBW     = kHi - kLo

    aNoise noise 0.5, 0
    aFilt  butterbp aNoise, kCenter, kBW
    ; Normalise roughly by bandwidth (narrower band = louder perceived)
    aFilt  = aFilt * sqrt(kBW / 100)
    aFilt  = aFilt * kGain * 0.1

    outs aFilt, aFilt
endin

; -----------------------------------------------------------------------
; instr 2 — arpeggiator
;   Schedules short sine tones at random MIDI pitches within [noteLo, noteHi].
;   Rate is fixed at one note every 0.18 s; increment = 1 keeps pitches
;   on semitones, so the range slider snaps to whole MIDI note numbers.
; -----------------------------------------------------------------------
instr 2
    kLo   chnget "noteLo"
    kHi   chnget "noteHi"
    kGain chnget "gain"
    kRateLo chnget "rateLo"
    kRateHi chnget "rateHi"
    
    kRate random kRateLo, kRateHi
    
    ; Guard: ensure Hi >= Lo
    kHi = (kHi >= kLo ? kHi : kLo)

    ; Trigger a new grain every kRate seconds
    kTrig  metro 1 / kRate

    if kTrig == 1 then
        ; Pick a random integer MIDI note in [kLo, kHi]
        knote = int(kLo + int(random:k(0, kHi - kLo + 1)))
        knote = limit(knote, kLo, kHi)
        kfreq = cpsmidinn(knote)
        ; Schedule a short sine note (instr 3) with this frequency
        schedulek 3, 0, kRate * 1.1, kfreq, kGain * 0.35
        ; Also schedule the noise together with the sine note
        schedulek 1, 0, 0.05
    endif
endin

; -----------------------------------------------------------------------
; instr 3 — single arpeggio note (sine + envelope)
;   p4 = frequency (Hz)
;   p5 = amplitude
; -----------------------------------------------------------------------
instr 3
    iFreq = p4
    iAmp  = p5
    iDur  = p3
    iAtt  = iDur * 0.08
    iRel  = iDur * 0.45

    aEnv  linseg 0, iAtt, iAmp, iDur - iAtt - iRel, iAmp, iRel, 0
    aSig  oscili aEnv, iFreq, 1
    outs  aSig, aSig
endin

</CsInstruments>
<CsScore>
; Sine wave table
f1 0 4096 10 1

; Start arpeggiator (runs indefinitely)
i2 0 z
</CsScore>
</CsoundSynthesizer>
