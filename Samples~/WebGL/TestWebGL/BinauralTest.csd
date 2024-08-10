<Cabbage>
form caption("Binaural Test") size(400, 300), guiMode("queue"), pluginId("bin1")
checkbox bounds(34, 30, 130, 54) channel("isWebGL") text("Is WebGL") value(1) fontColour:1(0, 255, 0, 255) 
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-odac
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 1
0dbfs = 1

giSine          ftgen       0, 0, 2^12, 10, 1
giSquare        ftgen       0, 0, 2^12, 10, 1, 0, 1, 0             
giLFOShape      ftgen       0, 0, 131072, 19, 0.5,1,180,1 ; U-shape parabola\

gS_HRTF_left   =           "hrtf-44100-left.dat"
gS_HRTF_right  =           "hrtf-44100-right.dat"

instr 1

seed 0
ifreq random 40, 600
irate random 1, 10

kAz   init 0
kElev init 0
kRoll init 1
    
; create an audio signal
krate          oscil       irate,0.2,giLFOShape            ; rate of impulses
; amplitude envelope: a repeating pulse
kEnv           loopseg     krate,0, 0,0, 0.015,1, 0.05, 0
aSig           oscili kEnv, ifreq,giSquare                            

; get the isWebGL channel once (changing it at runtime will have no effect)
iWebGL chnget "isWebGL"

// default values when it's not webGL - i.e. the editor maybe?
// remember to set "isWebGL" channel from the host!
if iWebGL == 0 then
; no binaural 3d processing
    out aSig;, aSig
else
    ; -- apply binaural 3d processing --
    ; azimuth (direction in the horizontal plane)
    kAz chnget "azimuth"
    ; elevation (direction in the vertical plane)
    kElev chnget "elevation"
    ; rolloff (volume by distance)
    kRoll chnget "rolloff"

; apply hrtfmove2 opcode to audio source - create stereo ouput
aLeft, aRight  hrtfmove2   aSig, kAz, kElev, gS_HRTF_left, gS_HRTF_right
               outs        aLeft * kRoll, aRight * kRoll            ; audio to outputs
endif

endin

</CsInstruments>
<CsScore>
i 1 0 z ; instr 1 plays forever
</CsScore>
</CsoundSynthesizer>
;original example by Iain McCurdy
; tweaked for CsoundUnity - WebGL by gb
