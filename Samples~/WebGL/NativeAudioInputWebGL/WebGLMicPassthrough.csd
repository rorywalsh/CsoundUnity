; WebGLMicPassthrough.csd
; The microphone, passed through to the output with a gain control channel.
;
; Channels:
;   "gain"   - output gain, range 0..2, default 1.0
;
; nchnls_i is 1 here, where the native sample reads 2. A browser hands out a mono stream from
; getUserMedia and caps it at two channels whatever the hardware has. The one input goes to both
; outputs, so the sample is heard on both sides instead of only the left.
;
; There is no -n and no sample rate here. On desktop and mobile CsoundUnity sets -n, -d and the
; sample rate itself, from Unity's audio settings. In the browser Csound opens the audio device
; and the browser decides the rate, which is 48000 on most machines.
;
; sr below is only a declaration for the browser. If a device runs at 44100 the pitch shifts,
; because the orchestra and the audio context disagree on the rate.

<CsoundSynthesizer>
<CsOptions>
-d
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

    aIn      inch 1
    aOut     = aIn * kGain
    out      aOut, aOut
endin

</CsInstruments>
<CsScore>
f0 z
i1 0 -1
</CsScore>
</CsoundSynthesizer>
