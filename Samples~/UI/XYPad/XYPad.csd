<Cabbage>
form caption("XYPad Example") size(400, 350), guiMode("queue"), colour(2, 145, 209) pluginId("def1")
xypad bounds(26, 20, 260, 250) channel("x", "y") rangeX(20, 800, 105) rangeY(20, 800, 200)
rslider bounds(300, 190, 73, 71) channel("gain") textColour(255, 255, 255, 255), text("Gain") range(0, 2, 1.158, 1, 0.001)
rslider bounds(300, 32, 73, 71) channel("wetDry") textColour(255, 255, 255, 255), text("Wet/Dry") range(0, 1, 0, 1, 0.001)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>e
<CsInstruments>
; Initialize the global variables. 
ksmps = 16
nchnls = 2
0dbfs = 1

; Rory Walsh 2021 
; ported to CsoundUnity by gb 2026
;
; License: CC0 1.0 Universal
; You can copy, modify, and distribute this work, 
; even for commercial purposes, all without asking permission. 

instr 1

    kX = chnget:k("x")
    kY = chnget:k("y")
    kSpeed = chnget:k("speed")

    aMod oscili 1, kX
    aCar oscili aMod, kY

    aDryL vco2 0.6, kX
    aDryR vco2 0.6, kY 
    aDelL comb aDryL, 5, 0.25
    aDelR comb aDryR, 5, 0.75

    aPan jspline 1, .1, .5

    aWetL, aWetR reverbsc (aDelL*aCar)*aPan, (aDelR*aCar)*(1-aPan), .7, 1000

    aL ntrpol aWetL, aDryL, chnget:k("wetDry")
    aR ntrpol aWetR, aDryR, chnget:k("wetDry")

    outs aL*chnget:k("gain"), aR*chnget:k("gain")


endin

</CsInstruments>
<CsScore>
;causes Csound to run for about 7000 years...
f0 z
;starts instrument 1 and runs it for a week
i1 0 z
</CsScore>
</CsoundSynthesizer>