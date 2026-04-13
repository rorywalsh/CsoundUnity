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
-n -d -+rtaudio=null
</CsOptions>
<CsInstruments>

sr      = 48000
ksmps   = 128
nchnls  = 2
nchnls_i = 1
0dbfs   = 1

; Gain control channel.
chn_k "gain", 3   ; 3 = read/write

instr 1
    kGain   chnget "gain"
    kGain   = (kGain == 0 ? 1 : kGain)   ; default 1 if not set

    aIn     inch 1               ; read from spin channel 1

    ; For stereo output, spread the mono input to both channels.
    aOut    = aIn * kGain
    outs    aOut, aOut
endin

</CsInstruments>
<CsScore>
; Start instrument 1 indefinitely.
i 1 0 -1
e
</CsScore>
</CsoundSynthesizer>
