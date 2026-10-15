<Cabbage>
form caption("Binaural Test") size(400, 300), guiMode("queue"), pluginId("bin1")
checkbox bounds(34, 30, 130, 54) channel("isWebGL") text("Is WebGL") value(1) fontColour:1(0, 255, 0, 255) 
hslider bounds(20, 110, 360, 40) channel("freq") range(40, 600, 220, 1, 0.1) text("Freq") valuetextbox(1)
hslider bounds(20, 170, 360, 40) channel("rate") range(1, 10, 4, 1, 0.01) text("Rate") valuetextbox(1)
</Cabbage>

/* Attribution-NonCommercial-ShareAlike 4.0 International
Attribution - You must give appropriate credit, provide a link to the license, and indicate if changes were made. You may do so in any reasonable manner, but not in any way that suggests the licensor endorses you or your use.
NonCommercial - You may not use the material for commercial purposes.
ShareAlike - If you remix, transform, or build upon the material, you must distribute your contributions under the same license as the original.
https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode */

; BinauralTest.csd
; Based on an HRTF binaural example by Iain McCurdy
; Changes: adapted for CsoundUnity on WebGL
; https://iainmccurdy.org
<CsoundSynthesizer>
<CsOptions>
-odac
</CsOptions>
<CsInstruments>

sr = 44100
ksmps = 32
nchnls = 2
0dbfs = 1

giSine          ftgen       0, 0, 2^12, 10, 1
giSquare        ftgen       0, 0, 2^12, 10, 1, 0, 1, 0             
giLFOShape      ftgen       0, 0, 131072, 19, 0.5,1,180,1 ; U-shape parabola\

gS_HRTF_left   =           "hrtf-44100-left.dat"
gS_HRTF_right  =           "hrtf-44100-right.dat"

instr 1

; Pitch and pulse rate come from the host, one setting per instance, so several of these in a scene
; are told apart by ear.

kAz   init 0
kElev init 0
kRoll init 1

; Three of these play into the same output and share one headroom. Measured, the three together
; peak at 0.71, because their pulses rarely land on the same sample: the arithmetic worst case of
; three times one peak does not happen. Left at unity, with the measurement as the reason.
iAmp = 1
    
; create an audio signal
kFreq          chnget      "freq"
kRate          chnget      "rate"

krate          oscil       kRate,0.2,giLFOShape            ; rate of impulses
; amplitude envelope: a repeating pulse
kEnv           loopseg     krate,0, 0,0, 0.015,1, 0.05, 0
aSig           oscili kEnv, kFreq,giSquare                            

; get the isWebGL channel once (changing it at runtime will have no effect)
iWebGL chnget "isWebGL"

// default values when it's not webGL - i.e. the editor maybe?
// remember to set "isWebGL" channel from the host!
if iWebGL == 0 then
; no binaural 3d processing
    out aSig * iAmp, aSig * iAmp
else
    ; -- apply binaural 3d processing --
    ; azimuth (direction in the horizontal plane)
    kAz chnget "azimuth"
    ; elevation (direction in the vertical plane)
    kElev chnget "elevation"
    ; rolloff (volume by distance)
    kRoll chnget "rolloff"

    ; The host sends these once a frame, so they arrive as steps. A gain applied in steps is zipper
    ; noise, heard as small interruptions rather than as movement, and an elevation that jumps makes
    ; the opcode swap measurement rings in one k. Twenty milliseconds of glide is below what the ear
    ; follows as movement and above what it hears as a step.
    ;
    ; Azimuth is left alone on purpose: it wraps, and gliding from 359 to 0 would travel the long
    ; way round through every angle.
    kElev portk kElev, 0.02
    kRoll portk kRoll, 0.02

; apply hrtfmove2 opcode to audio source - create stereo ouput
aLeft, aRight  hrtfmove2   aSig, kAz, kElev, gS_HRTF_left, gS_HRTF_right
               outs        aLeft * kRoll * iAmp, aRight * kRoll * iAmp   ; audio to outputs
endif

endin

</CsInstruments>
<CsScore>
i 1 0 z ; instr 1 plays forever
</CsScore>
</CsoundSynthesizer>
;original example by Iain McCurdy
; tweaked for CsoundUnity - WebGL by gb
