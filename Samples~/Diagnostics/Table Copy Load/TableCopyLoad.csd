<CsoundSynthesizer>
<CsOptions>
-d
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs = 1

; Fixed tables avoid compilation or table replacement during a measured phase.
giSmall  ftgen 100, 0, 4096, 10, 1
giMedium ftgen 101, 0, 65536, 10, 1
giLarge  ftgen 102, 0, 1048576, 10, 1

chnset 100, "table"
chnset 1, "voices"

instr 1
    iVoice = p4
    kVoices chnget "voices"
    kTable chnget "table"
    ; Smooth voice-count changes before the measured phases.
    ; Equal-power scaling keeps RMS level comparable as voices are added.
    ; At 64 voices the sum of all peak amplitudes is at most 0.64.
    kTarget = (iVoice <= kVoices ? 0.08 / sqrt(max(kVoices, 1)) : 0)
    kAmplitude portk kTarget, 0.01
    aAmplitude interp kAmplitude
    if kAmplitude > 0.0000001 then
        ; One phase accumulator follows the voice across all table choices. Separate
        ; oscillators in these branches would restart at unrelated phases when switching.
        aPhase phasor 110 + iVoice * 3
        if kTable == 100 then
            aWave tablei aPhase, 100, 1
        elseif kTable == 101 then
            aWave tablei aPhase, 101, 1
        else
            aWave tablei aPhase, 102, 1
        endif
        aTone = aWave * aAmplitude
        out aTone, aTone
    endif
endin

iVoice = 1
while iVoice <= 64 do
    ; Fractional instrument IDs keep indefinite notes distinct.
    schedule 1 + iVoice / 1000.0, 0, -1, iVoice
    iVoice += 1
od
</CsInstruments>
<CsScore>
f0 z
</CsScore>
</CsoundSynthesizer>
