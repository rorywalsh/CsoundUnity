; NativeInputPassthrough.csd
; Minimal CSD for testing CsoundUnity native audio input.
;
; Reads from the audio input (fed by CsoundUnityNativeAudioInput via the spin buffer)
; and passes it to the output with a gain control channel.
;
; Channels:
;   "gain"   - output gain, range 0..2, default 1.0
;   "in1".."in4" - the level arriving on each input, read only, so the inspector shows
;                  which inputs are live without having to guess by ear
;
; nchnls_i is how many input channels the orchestra reads. CsoundUnityNativeAudioInput fills as
; many of them as the device actually has, so on a stereo input channels 3 and 4 stay silent
; and their levels read zero — the on-screen panel says so when it happens.

<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>

sr      = 48000
ksmps   = 128
nchnls  = 2
nchnls_i = 4
0dbfs   = 1

instr 1
    kGain   chnget "gain"
    if kGain == 0 then
        kGain = 1
    endif

    a1      inch 1               ; read from spin channel 1
    a2      inch 2
    a3      inch 3
    a4      inch 4

    ; The level of each input, so four channels can be told apart while testing.
    chnset  rms(a1), "in1"
    chnset  rms(a2), "in2"
    chnset  rms(a3), "in3"
    chnset  rms(a4), "in4"

    ; Odd inputs to the left, even to the right. Nothing is filled in from a neighbour:
    ; this sample exists to show the input mapping, so a silent channel stays audibly silent.
    aOutL   = (a1 + a3) * kGain
    aOutR   = (a2 + a4) * kGain
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
