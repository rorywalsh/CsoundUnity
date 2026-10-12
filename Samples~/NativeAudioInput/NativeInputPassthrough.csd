; NativeInputPassthrough.csd
; Minimal CSD for testing CsoundUnity native audio input.
;
; Reads from the audio input (fed by NativeAudioInputManager via the spin buffer)
; and passes it to the output with a gain control channel.
;
; Channels:
;   "gain"   - output gain, range 0..2, default 1.0
;
; nchnls_i is how many input channels the orchestra reads. NativeAudioInputManager fills as
; many of them as the device actually has, so on a mono input channel 2 stays silent and only
; the left side is heard — the on-screen panel says so when it happens.

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

sr      = 48000
ksmps   = 128
nchnls  = 2
nchnls_i = 2
0dbfs   = 1

instr 1
    kGain   chnget "gain"
    if kGain == 0 then
        kGain = 1
    endif

    aInL     inch 1               ; read from spin channel 1
    aInR     inch 2
    ; Straight through, channel for channel: this sample exists to show the input mapping,
    ; so a silent channel stays audibly silent instead of being filled in from the other one.
    aOutL    = aInL * kGain
    aOutR    = aInR * kGain
    out     aOutL, aOutR
endin

</CsInstruments>
<CsScore>
; f0 z extends the score to an astronomically large time so Csound never
; stops on its own. i 1 0 z starts the instrument for the same duration.
; (Csound 7 terminates on "e" even with -1-duration instruments — use z instead.)
f0 z
i 1 0 z
</CsScore>
</CsoundSynthesizer>
