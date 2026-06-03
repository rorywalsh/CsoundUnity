; NativeInputPassthrough.csd
; Minimal CSD for testing CsoundUnity native audio input.
;
; Reads from the audio input (fed by NativeAudioInputManager via the spin buffer)
; and passes it to the output with a gain control channel.
;
; Channels:
;   "gain"   - output gain, range 0..2, default 1.0
;
; nchnls_i must match the number of channels you open in NativeAudioInputManager.
; Change it to 2 for stereo testing.

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

sr      = 48000
ksmps   = 128
nchnls  = 2
nchnls_i = 1
0dbfs   = 1

instr 1
    kGain   chnget "gain"
    if kGain == 0 then
        kGain = 1
    endif

    aIn     inch 1               ; read from spin channel 1

    ; For stereo output, spread the mono input to both channels.
    aOut    = aIn * kGain
    out     aOut, aOut
endin

</CsInstruments>
<CsScore>
; Start instrument 1 indefinitely.
f0 z
i 1 0 z
</CsScore>
</CsoundSynthesizer>
