<CsoundSynthesizer>
<CsOptions>
-d
</CsOptions>
<CsInstruments>
; Not one of these values is a default, so a getter that falls back is visible in the report:
; a fallback says 2 channels, ksmps 32, 0dbfs 32768, and nchnls_i equal to nchnls.
sr       = 48000
ksmps    = 64
nchnls   = 4
nchnls_i = 1
0dbfs    = 1

; Four distinct tones identify the engine output channels. The recording and device
; routing determine which of these reach the final output.
instr 1
    kFade linseg 0, 0.1, 1
    a1 oscili 0.1 * kFade, 220
    a2 oscili 0.1 * kFade, 277.183
    a3 oscili 0.1 * kFade, 329.628
    a4 oscili 0.1 * kFade, 440
    outch 1, a1, 2, a2, 3, a3, 4, a4
endin

; A counter the scene reads back: it tells a performance that runs from one that was
; never started, which on WebGL waits for the page's first gesture.
instr 2
    kTick init 0
    kTick += 1
    chnset kTick, "tick"
endin
</CsInstruments>
<CsScore>
f 0 z
i 1 0 -1
i 2 0 -1
</CsScore>
</CsoundSynthesizer>
